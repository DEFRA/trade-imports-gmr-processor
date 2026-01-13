namespace GmrProcessor.Data;

public interface IMessageAuditRepository
{
    Task<List<MessageAudit>> GetByMessageTypeAsync(
        string messageType,
        DateTime fromTimestamp,
        CancellationToken cancellationToken
    );
}
