using GmrProcessor.Data.Auditing;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.Eta;
using GmrProcessor.Data.Gto;
using GmrProcessor.Data.ImportGmrMatching;
using GmrProcessor.Data.Matching;

namespace GmrProcessor.Data;

public interface IMongoContext
{
    IGtoGmrCollection GtoGmr { get; }
    IMongoCollectionSet<MatchedGmrItem> GtoMatchedGmrItem { get; }
    IEtaGmrCollection EtaGmr { get; }
    IMongoCollectionSet<ImportTransit> ImportTransits { get; }
    IMongoCollectionSet<MatchedImportNotification> MatchedImportNotifications { get; }
    IMongoCollectionSet<MrnChedMatch> MrnChedMatches { get; }
    IMongoCollectionSet<MessageAudit> MessageAudits { get; }
}
