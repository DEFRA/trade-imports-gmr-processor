using MongoDB.Driver;

namespace GmrProcessor.Data;

public interface IMatchedGmrItemCollection : IMongoCollectionSet<MatchedGmrItem>
{
    Task<UpdateResult> UpdateOrInsert(MatchedGmrItem matchedGmrItem, CancellationToken cancellationToken);
}
