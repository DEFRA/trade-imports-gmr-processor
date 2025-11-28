namespace GmrProcessor.Data;

public interface IMongoContext
{
    IMongoCollectionSet<ImportTransit> ImportTransits { get; }
    IMatchedGmrItemCollection GtoMatchedGmrItem { get; }
}
