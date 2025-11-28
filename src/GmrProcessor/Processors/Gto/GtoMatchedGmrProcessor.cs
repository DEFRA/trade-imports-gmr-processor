using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Data;
using GmrProcessor.Extensions;

namespace GmrProcessor.Processors.Gto;

public class GtoMatchedGmrProcessor(ILogger<GtoMatchedGmrProcessor> logger, IMongoContext mongoContext)
    : IGtoMatchedGmrProcessor
{
    public async Task Process(MatchedGmr matchedGmr, CancellationToken cancellationToken)
    {
        var importTransit = await mongoContext.ImportTransits.FindOne(
            it => it.Mrn == matchedGmr.Mrn,
            cancellationToken
        );

        if (importTransit is null)
        {
            logger.LogInformation("No import transit found for Mrn: {Mrn}, skipping", matchedGmr.Mrn);
            return;
        }

        var item = new MatchedGmrItem
        {
            ImportTransitId = importTransit.Id,
            Mrn = matchedGmr.Mrn,
            GmrId = matchedGmr.Gmr.GmrId,
            Gmr = matchedGmr.Gmr,
            UpdatedDateTime = matchedGmr.Gmr.GetUpdatedDateTime(),
        };

        var result = await mongoContext.GtoMatchedGmrItem.UpdateOrInsert(item, cancellationToken);

        if (!result.IsUpdatedOrNew())
        {
            logger.LogInformation(
                "Skipping an old GMR item, Mrn: {Mrn}, Ched: {ChedId}, Gmr: {GmrId}, UpdatedTime: {UpdatedTime}",
                item.Mrn,
                item.ImportTransitId,
                item.Gmr.GmrId,
                item.Gmr.UpdatedDateTime
            );
            return;
        }

        logger.LogInformation(
            "New matched GMR item {InsertedOrUpdated}, Mrn: {Mrn}, Ched: {ChedId}, Gmr: {GmrId}, UpdatedTime: {UpdatedTime}",
            result.UpsertedId is not null ? "inserted" : "updated",
            item.Mrn,
            item.ImportTransitId,
            item.Gmr.GmrId,
            item.Gmr.UpdatedDateTime
        );
    }
}
