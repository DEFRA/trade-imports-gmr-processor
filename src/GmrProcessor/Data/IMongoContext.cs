namespace GmrProcessor.Data;

public interface IMongoContext
{
    IMongoCollectionSet<ImportTransit> ImportTransits { get; }

    IMongoCollectionSet<MatchedImportNotification> MatchedImportNotifications { get; }

    IMatchedGmrItemCollection GtoMatchedGmrItem { get; }
}
