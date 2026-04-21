using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.Gto;
using GmrProcessor.Logging;
using GmrProcessor.Services;
using GmrProcessor.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GmrProcessor.Processors.Gto;

public partial class GtoImportPreNotificationProcessor(
    IMongoContext mongoContext,
    ILogger<GtoImportPreNotificationProcessor> logger,
    IGtoMatchedGmrCollection matchedGmrCollection,
    IGvmsHoldService gvmsHoldService
) : IGtoImportPreNotificationProcessor
{
    private readonly ILogger<GtoImportPreNotificationProcessor> _logger =
        new PrefixedLogger<GtoImportPreNotificationProcessor>(logger, "GTO");

    public async Task<GtoImportNotificationProcessorResult> Process(
        ResourceEvent<ImportPreNotificationEvent> importPreNotificationEvent,
        CancellationToken cancellationToken
    )
    {
        var reference = importPreNotificationEvent.ResourceId;
        var importPreNotification = importPreNotificationEvent.Resource!.ImportPreNotification!;

        var importTransitResult = TransitValidation.IsTransit(importPreNotification);

        if (!importTransitResult.IsTransit)
        {
            _logger.LogInformation("CHED {ChedId} is not a transit, skipping", reference);
            return GtoImportNotificationProcessorResult.SkippedNotATransit;
        }

        var transitOverride = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        var filter = Builders<ImportTransit>.Filter.Eq(x => x.Id, reference);
        var update = Builders<ImportTransit>
            .Update.SetOnInsert(x => x.Id, reference)
            .Set(x => x.TransitOverrideRequired, transitOverride.IsOverrideRequired)
            .Set(x => x.Mrn, importTransitResult.Mrn);
        var options = new FindOneAndUpdateOptions<ImportTransit>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.Before,
        };

        await mongoContext.ImportTransits.FindOneAndUpdate(filter, update, options, cancellationToken);

        _logger.LogInformation("Inserted or updated ImportTransit {Id}", reference);

        var mrn = importTransitResult.Mrn!;

        var matchedGmrs = await matchedGmrCollection.GetAllByMrn(mrn, cancellationToken);
        if (matchedGmrs.Count == 0)
        {
            _logger.LogInformation("Tried to place or release hold on MRN {Mrn} but no MatchedGmr exists", mrn);
            return GtoImportNotificationProcessorResult.NoMatchedGmrExists;
        }

        var processorResult = GtoImportNotificationProcessorResult.NoHoldChange;
        foreach (var matchedGmr in matchedGmrs)
        {
            var result = await gvmsHoldService.PlaceOrReleaseHold(matchedGmr.GmrId, cancellationToken);
            processorResult = result switch
            {
                GvmsHoldResult.HoldPlaced => GtoImportNotificationProcessorResult.HoldPlaced,
                GvmsHoldResult.HoldReleased => GtoImportNotificationProcessorResult.HoldReleased,
                GvmsHoldResult.NoHoldChange => GtoImportNotificationProcessorResult.NoHoldChange,
                _ => throw new InvalidOperationException($"Unexpected GvmsHoldResult value: {result}"),
            };

            LogHoldMessageContext(processorResult, matchedGmr, mrn, reference, importPreNotification);
        }

        return processorResult;
    }

    private void LogHoldMessageContext(
        GtoImportNotificationProcessorResult processorResult,
        MatchedGmrItem matchedGmr,
        string mrn,
        string reference,
        ImportPreNotification importPreNotification
    )
    {
        if (processorResult == GtoImportNotificationProcessorResult.HoldPlaced)
        {
            logger.LogInformation(
                "Hold placed on GMR {Details}",
                new HoldDecisionLogContext
                {
                    ChedReference = reference,
                    Mrn = mrn,
                    Gmr = matchedGmr.GmrId,
                    PortOfEntry = importPreNotification.PartOne?.PortOfEntry,
                    PortOfExit = importPreNotification.PartOne?.PortOfExit,
                    CountryOfOrigin = importPreNotification.PartOne?.Commodities?.CountryOfOrigin,
                    CountryOfDestination = importPreNotification.PartOne?.Commodities?.ConsignedCountry,
                    ChedType = importPreNotification.ImportNotificationType,
                    ProvideCtcMrn = importPreNotification.PartOne?.ProvideCtcMrn,
                    PurposeGroup = importPreNotification.PartOne?.Purpose?.PurposeGroup,
                    PurposeThirdCountry = importPreNotification.PartOne?.Purpose?.ThirdCountry,
                    Timestamp = DateTime.UtcNow,
                }.ToJson()
            );
        }
    }

    private sealed class HoldDecisionLogContext
    {
        public string? ChedReference { get; set; }
        public string? Mrn { get; set; }
        public string? Gmr { get; set; }
        public string? PortOfEntry { get; set; }
        public string? PortOfExit { get; set; }
        public string? CountryOfOrigin { get; set; }
        public string? CountryOfDestination { get; set; }
        public string? ChedType { get; set; }
        public string? ProvideCtcMrn { get; set; }
        public string? PurposeGroup { get; set; }
        public string? PurposeThirdCountry { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
