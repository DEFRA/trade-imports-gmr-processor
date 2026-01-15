using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrProcessor.Data.Gto;
using GmrProcessor.Extensions;
using GmrProcessor.Services;

namespace GmrProcessor.Processors.Gto;

public class GtoMatchedGmrProcessor(
    ILogger<GtoMatchedGmrProcessor> logger,
    IGtoMatchedGmrCollection matchedGmrCollection,
    IGtoGmrCollection gtoGmrCollection,
    IGtoImportTransitCollection gtoImportTransitCollection,
    IGvmsHoldService gvmsHoldService
) : IGtoMatchedGmrProcessor
{
    public async Task<GtoMatchedGmrProcessorResult> Process(MatchedGmr matchedGmr, CancellationToken cancellationToken)
    {
        var importTransit = await gtoImportTransitCollection.GetByMrn(matchedGmr.Mrn!, cancellationToken);
        if (importTransit is null)
        {
            logger.LogInformation("Skipping {Mrn} because no import transit was found", matchedGmr.Mrn);
            return GtoMatchedGmrProcessorResult.SkippedNoTransit;
        }

        var gtoGmr = await gtoGmrCollection.UpdateOrInsert(BuildGtoGmr(matchedGmr.Gmr), cancellationToken);
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

        await matchedGmrCollection.UpsertMatchedItem(matchedGmr, cancellationToken);

        var result = await gvmsHoldService.PlaceOrReleaseHold(matchedGmr.Gmr.GmrId, cancellationToken);
        return result switch
        {
            GvmsHoldResult.NoHoldChange => GtoMatchedGmrProcessorResult.NoHoldChange,
            GvmsHoldResult.HoldPlaced => GtoMatchedGmrProcessorResult.HoldPlaced,
            GvmsHoldResult.HoldReleased => GtoMatchedGmrProcessorResult.HoldReleased,
            _ => throw new InvalidOperationException($"Unexpected GvmsHoldResult value: {result}"),
        };
    }

    private static GtoGmr BuildGtoGmr(Gmr gmr) =>
        new()
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };
}
