using System.Diagnostics.CodeAnalysis;
using GmrProcessor.Data.Gto;
using GmrProcessor.Utils.Mongo;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class MongoContext(IMongoDbClientFactory database) : IMongoContext
{
    public IMongoCollectionSet<ImportTransit> ImportTransits { get; } = new MongoCollectionSet<ImportTransit>(database);
    public IGtoGmrCollection GtoGmr { get; } = new GtoGmrCollection(database);
    public IMongoCollectionSet<MatchedGmrItem> GtoMatchedGmrItem { get; } = new GtoMatchedItemCollection(database);
}
