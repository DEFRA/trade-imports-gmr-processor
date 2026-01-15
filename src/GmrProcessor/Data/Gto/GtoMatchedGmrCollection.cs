using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Extensions;
using MongoDB.Driver;

namespace GmrProcessor.Data.Gto;

public class GtoMatchedGmrCollection(IMongoContext mongo) : IGtoMatchedGmrCollection
{
    public Task UpsertMatchedItem(MatchedGmr matchedGmr, CancellationToken cancellationToken)
    {
        var filter = Builders<MatchedGmrItem>.Filter.Where(f =>
            f.GmrId == matchedGmr.Gmr.GmrId && f.Mrn == matchedGmr.Mrn
        );

        var update = Builders<MatchedGmrItem>
            .Update.Set(f => f.GmrId, matchedGmr.Gmr.GmrId)
            .Set(f => f.Mrn, matchedGmr.Mrn)
            .Set(f => f.UpdatedDateTime, matchedGmr.Gmr.GetUpdatedDateTime());

        return mongo.GtoMatchedGmrItem.UpdateOne(
            filter,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken
        );
    }

    public async Task<List<string>> GetRelatedMrns(string gmrId, CancellationToken cancellationToken)
    {
        var relatedMatchedGmrItems = await mongo.GtoMatchedGmrItem.FindMany<MatchedGmrItem>(
            f => f.GmrId == gmrId,
            cancellationToken
        );

        return relatedMatchedGmrItems.Where(x => x.Mrn is not null).Select(x => x.Mrn!).ToList();
    }

    public async Task<MatchedGmrItem?> GetByMrn(string mrn, CancellationToken cancellationToken)
    {
        return await mongo.GtoMatchedGmrItem.FindOne(g => g.Mrn == mrn, cancellationToken);
    }
}
