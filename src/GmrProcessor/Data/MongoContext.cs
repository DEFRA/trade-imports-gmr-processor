using System.Diagnostics.CodeAnalysis;
using GmrProcessor.Utils.Mongo;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class MongoContext(IMongoDbClientFactory database) : IMongoContext
{
    public IMongoCollectionSet<ImportTransit> ImportTransits { get; } = new MongoCollectionSet<ImportTransit>(database);
    public IMatchedGmrItemCollection GtoMatchedGmrItem { get; } = new GtoMatchedGmrItemCollection(database);
}
