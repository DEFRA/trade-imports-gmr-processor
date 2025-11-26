using MongoDB.Driver;

namespace GmrProcessor.Data;

public interface IMongoContext
{
    IMongoCollectionSet<ImportTransit> ImportTransits { get; }
}
