using MongoDB.Driver;

namespace GmrProcessor.Data;

public class ImportTransit : IDataEntity
{
    public required string Id { get; set; }
}

public interface IMongoContext
{
    IMongoCollectionSet<ImportTransit> ImportTransits { get; }
}
