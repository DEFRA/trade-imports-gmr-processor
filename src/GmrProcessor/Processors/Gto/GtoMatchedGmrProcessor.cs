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
    IGtoImportTransitRepository gtoImportTransitRepository,
    IGvmsApiClientService gvmsApiClientService
) : IGtoMatchedGmrProcessor
{
    public async Task<GtoMatchedGmrProcessorResult> Process(MatchedGmr matchedGmr, CancellationToken cancellationToken)
    {
        var importTransit = await gtoImportTransitRepository.GetByMrn(matchedGmr.Mrn!, cancellationToken);
        if (importTransit is null)
        {
            logger.LogInformation("Skipping {Mrn} because no import transit was found", matchedGmr.Mrn);
            return GtoMatchedGmrProcessorResult.SkippedNoTransit;
        }

        var gtoGmr = await matchedGmrRepository.UpsertGmr(BuildGtoGmr(matchedGmr.Gmr), cancellationToken);
        if (gtoGmr.UpdatedDateTime != matchedGmr.Gmr.GetUpdatedDateTime())
        {
            logger.LogInformation(
                "Skipping {Mrn} because it is an old GTO GMR item, Gmr: {GmrId}, UpdatedTime: {UpdatedTime}",
                matchedGmr.Mrn,
                matchedGmr.Gmr.GmrId,
                matchedGmr.Gmr.UpdatedDateTime
            );
            return GtoMatchedGmrProcessorResult.SkippedOldGmr;
        }

        logger.LogInformation(
            "Matched GMR item inserted/updated, Mrn: {Mrn}, Gmr: {GmrId}, UpdatedTime: {UpdatedTime}",
            matchedGmr.Mrn,
            matchedGmr.Gmr.GmrId,
            matchedGmr.Gmr.UpdatedDateTime
        );

        await matchedGmrRepository.UpsertMatchedItem(matchedGmr, cancellationToken);

        var relatedMrns = await matchedGmrRepository.GetRelatedMrns(matchedGmr.Gmr.GmrId, cancellationToken);
        var relatedImportTransits = await gtoImportTransitRepository.GetByMrns(relatedMrns, cancellationToken);
        var anyImportTransitsRequireHold = ShouldHold(relatedImportTransits);

        if (gtoGmr.HoldStatus)
        {
            logger.LogInformation("Matched GMR {GmrId} is already on hold, no action was taken", matchedGmr.Gmr.GmrId);
            return GtoMatchedGmrProcessorResult.NoHoldChange;
        }

        await gvmsApiClientService.PlaceOrReleaseHold(
            matchedGmr.Gmr.GmrId,
            anyImportTransitsRequireHold,
            cancellationToken
        );
        return GtoMatchedGmrProcessorResult.HoldPlaced;
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
