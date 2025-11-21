using GmrProcessor.Utils.Mongo;

namespace GmrProcessor.Data;

public class MongoContext(IMongoDbClientFactory database) : IMongoContext
{
    public IMongoCollectionSet<ImportTransit> ImportTransits { get; } = new MongoCollectionSet<ImportTransit>(database);
}
