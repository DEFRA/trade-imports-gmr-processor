namespace GmrProcessor.Data.Auditing;

public class MessageAuditRepository(IMongoContext mongoContext) : IMessageAuditRepository
{
    public async Task<List<MessageAudit>> GetByMessageTypeAsync(
        string messageType,
        DateTime fromTimestamp,
        CancellationToken cancellationToken
    )
    {
        return await mongoContext.MessageAudits.FindMany<MessageAudit>(
            x => x.MessageType == messageType && x.Timestamp >= fromTimestamp,
            cancellationToken
        );
    }
}
