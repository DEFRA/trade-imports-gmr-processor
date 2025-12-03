using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrProcessor.Data;
using GmrProcessor.Data.Gto;
using GmrProcessor.Extensions;
using GmrProcessor.Services;

namespace GmrProcessor.Processors.Gto;

public class GtoMatchedGmrProcessor(
    ILogger<GtoMatchedGmrProcessor> logger,
    IGtoMatchedGmrRepository matchedGmrRepository,
    IImportTransitRepository importTransitRepository,
    IGvmsApiClientService gvmsApiClientService
) : IGtoMatchedGmrProcessor
{
    public async Task<GtoMatchedGmrProcessResult> Process(MatchedGmr matchedGmr, CancellationToken cancellationToken)
    {
        var importTransit = await importTransitRepository.GetByMrn(matchedGmr.Mrn!, cancellationToken);
        if (importTransit is null)
        {
            logger.LogInformation("No import transit found for Mrn: {Mrn}, skipping", matchedGmr.Mrn);
            return GtoMatchedGmrProcessResult.SkippedNoTransit;
        }

        var gtoGmr = await matchedGmrRepository.UpsertGmr(BuildGtoGmr(matchedGmr.Gmr), cancellationToken);
        if (gtoGmr.UpdatedDateTime != matchedGmr.Gmr.GetUpdatedDateTime())
        {
            logger.LogInformation(
                "Skipping an old GMR item, Mrn: {Mrn}, Gmr: {GmrId}, UpdatedTime: {UpdatedTime}",
                matchedGmr.Mrn,
                matchedGmr.Gmr.GmrId,
                matchedGmr.Gmr.UpdatedDateTime
            );
            return GtoMatchedGmrProcessResult.SkippedOldGmr;
        }

        logger.LogInformation(
            "Matched GMR item inserted/updated, Mrn: {Mrn}, Gmr: {GmrId}, UpdatedTime: {UpdatedTime}",
            matchedGmr.Mrn,
            matchedGmr.Gmr.GmrId,
            matchedGmr.Gmr.UpdatedDateTime
        );

        await matchedGmrRepository.UpsertMatchedItem(matchedGmr, cancellationToken);

        var relatedMrns = await matchedGmrRepository.GetRelatedMrns(matchedGmr.Gmr.GmrId, cancellationToken);
        var relatedImportTransits = await importTransitRepository.GetByMrns(relatedMrns, cancellationToken);
        var anyImportTransitsRequireHold = ShouldHold(relatedImportTransits);

        if (gtoGmr.HoldStatus == anyImportTransitsRequireHold)
        {
            logger.LogInformation(
                "GMR {GmrId} is already in the correct hold state {HoldStatus}",
                matchedGmr.Gmr.GmrId,
                gtoGmr.HoldStatus
            );
            return GtoMatchedGmrProcessResult.NoHoldChange;
        }

        await gvmsApiClientService.PlaceOrReleaseHold(
            matchedGmr.Gmr.GmrId,
            anyImportTransitsRequireHold,
            cancellationToken
        );

        return anyImportTransitsRequireHold
            ? GtoMatchedGmrProcessResult.HoldPlaced
            : GtoMatchedGmrProcessResult.HoldReleased;
    }

    private static bool ShouldHold(IEnumerable<ImportTransit> relatedImportTransits) =>
        relatedImportTransits.Any(x => x.TransitOverrideRequired);

    private static GtoGmr BuildGtoGmr(Gmr gmr) =>
        new()
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };
}
