using GmrProcessor.Data.Eta;
using GmrProcessor.Data.Gto;

namespace GmrProcessor.Data;

public interface IMongoContext
{
    IGtoGmrCollection GtoGmr { get; }
    IMongoCollectionSet<MatchedGmrItem> GtoMatchedGmrItem { get; }
    IEtaGmrCollection EtaGmr { get; }
    IMongoCollectionSet<ImportTransit> ImportTransits { get; }
    IMongoCollectionSet<MatchedImportNotification> MatchedImportNotifications { get; }
    IMongoCollectionSet<MessageAudit> MessageAudits { get; }
}
