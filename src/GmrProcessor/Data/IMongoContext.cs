using GmrProcessor.Data.Gto;

namespace GmrProcessor.Data;

public interface IMongoContext
{
    IMongoCollectionSet<ImportTransit> ImportTransits { get; }
    IMongoCollectionSet<MatchedImportNotification> MatchedImportNotifications { get; }
    IGtoGmrCollection GtoGmr { get; }
    IMongoCollectionSet<MatchedGmrItem> GtoMatchedGmrItem { get; }
}
