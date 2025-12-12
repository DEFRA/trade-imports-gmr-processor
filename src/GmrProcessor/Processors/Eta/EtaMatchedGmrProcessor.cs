using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrProcessor.Data.Eta;
using GmrProcessor.Domain.Eta;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Gto;

namespace GmrProcessor.Processors.Eta;

public class EtaMatchedGmrProcessor(
    ILogger<EtaMatchedGmrProcessor> logger,
    IGtoImportTransitRepository importTransitRepository,
    ITradeImportsDataApiClient tradeImportsDataApi,
    IEtaGmrCollection etaGmrCollection
) : IEtaMatchedGmrProcessor
{
    private const string StateEmbarked = "EMBARKED";

    public async Task<EtaMatchedGmrProcessorResult> Process(MatchedGmr matchedGmr, CancellationToken cancellationToken)
    {
        if (!IsEmbarked(matchedGmr))
        {
            logger.LogInformation(
                "GMR {GmrId} status {Status} is not {StateEmbarked}, skipping",
                matchedGmr.Gmr.GmrId,
                matchedGmr.Gmr.State,
                StateEmbarked
            );
            return EtaMatchedGmrProcessorResult.SkippedNotEmbarked;
        }

        logger.LogInformation("Processing ETA for GMR {GmrId}", matchedGmr.Gmr.GmrId);

        var oldEtaGmrRecord = await etaGmrCollection.UpdateOrInsert(BuildGtoGmr(matchedGmr.Gmr), cancellationToken);
        if (!HasUpdatedCheckedInTime(matchedGmr.Gmr, oldEtaGmrRecord?.Gmr))
        {
            logger.LogInformation(
                "Skipping an old CheckedInTime ETA GMR item, Gmr: {GmrId}, Mrn: {Mrn}, UpdatedTime: {UpdatedTime}",
                matchedGmr.Gmr.GmrId,
                matchedGmr.Mrn,
                matchedGmr.Gmr.UpdatedDateTime
            );
            return EtaMatchedGmrProcessorResult.SkippedOldGmr;
        }

        var matchMrn = matchedGmr.Mrn!;
        var getImportsTask = GetImportsByMrn(matchMrn, cancellationToken);
        var getTransitsTask = GetTransitByMrn(matchMrn, cancellationToken);

        await Task.WhenAll(getImportsTask, getTransitsTask);

        List<ChedMrn> chedMrns = [.. getImportsTask.Result, .. getTransitsTask.Result];

        if (chedMrns.Count == 0)
        {
            logger.LogInformation("No CHEDs found for GMR {GmrId}, skipping", matchedGmr.Gmr.GmrId);
            return EtaMatchedGmrProcessorResult.NoChedsFound;
        }

        logger.LogInformation(
            "Informing Ipaffs of {Action} for CHEDs {ChedReferences} for GMR {GmrId} with ETA {Eta}",
            oldEtaGmrRecord is null ? "new arrival time" : "update to arrival time",
            string.Join(",", chedMrns),
            matchedGmr.Gmr.GmrId,
            matchedGmr.Gmr.CheckedInCrossing?.LocalDateTimeOfArrival
        );

        var ipaffsMessages = chedMrns.Select(chedMrn => new IpaffsUpdatedTimeOfArrivalMessage
        {
            LocalDateTimeOfArrival = ParseArrivalTimestamp(matchedGmr.Gmr.CheckedInCrossing?.LocalDateTimeOfArrival!),
            Mrn = chedMrn.Mrn,
            ReferenceNumber = chedMrn.ChedReference,
        });

        foreach (var message in ipaffsMessages)
        {
            logger.LogInformation("I would now send: {Message}", JsonSerializer.Serialize(message));
        }

        return EtaMatchedGmrProcessorResult.UpdatedIpaffs;
    }

    private static bool IsEmbarked(MatchedGmr matchedGmr) => matchedGmr.Gmr.State == StateEmbarked;

    private static bool HasUpdatedCheckedInTime(Gmr newGmr, Gmr? oldGmrRecord)
    {
        if (oldGmrRecord is null)
            return true;
        var newCheckedInTime = ParseArrivalTimestamp(newGmr.CheckedInCrossing!.LocalDateTimeOfArrival);
        var oldCheckedInTime = ParseArrivalTimestamp(oldGmrRecord.CheckedInCrossing!.LocalDateTimeOfArrival);
        return newCheckedInTime > oldCheckedInTime;
    }

    private static DateTime ParseArrivalTimestamp(string input)
    {
        return DateTime.ParseExact(
            input,
            "yyyy-MM-dd'T'HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
    }

    private static EtaGmr BuildGtoGmr(Gmr gmr) =>
        new()
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };

    private async Task<List<ChedMrn>> GetTransitByMrn(string mrn, CancellationToken cancellationToken)
    {
        return (await importTransitRepository.GetByMrns([mrn], cancellationToken))
            .Select(it => new ChedMrn { ChedReference = it.Id, Mrn = it.Mrn! })
            .ToList();
    }

    private async Task<List<ChedMrn>> GetImportsByMrn(string matchMrn, CancellationToken cancellationToken)
    {
        var importPreNotifications = await tradeImportsDataApi.GetImportPreNotificationsByMrn(
            matchMrn,
            cancellationToken
        );

        return importPreNotifications
            .ImportPreNotifications.Where(i =>
                i.ImportPreNotification.ExternalReferences is { Length: > 0 }
                && i.ImportPreNotification.ExternalReferences.Any(r => r.Reference is not null)
            )
            .Select(i => new ChedMrn
            {
                ChedReference = i.ImportPreNotification.ReferenceNumber!,
                Mrn = i.ImportPreNotification.ExternalReferences!.Select(r => r.Reference).First()!,
            })
            .ToList();
    }

    [ExcludeFromCodeCoverage]
    private sealed record ChedMrn
    {
        public required string ChedReference { get; init; }
        public required string Mrn { get; init; }
    }
}
