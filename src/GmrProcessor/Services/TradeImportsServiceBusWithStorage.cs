using System.Text.Json;
using GmrProcessor.Data;
using GmrProcessor.Data.Auditing;
using MongoDB.Driver;

namespace GmrProcessor.Services;

public class TradeImportsServiceBusWithStorage(
    ITradeImportsServiceBus serviceBus,
    IMongoContext mongoContext,
    ILogger<TradeImportsServiceBusWithStorage> logger
) : ITradeImportsServiceBus
{
    public async Task SendMessagesAsync<T>(
        IEnumerable<T> messages,
        string queueName,
        CancellationToken cancellationToken = default
    )
    {
        var messagesList = messages.ToList();

        await serviceBus.SendMessagesAsync(messagesList, queueName, cancellationToken);

        try
        {
            var timestamp = DateTime.UtcNow;
            var messageType = typeof(T).Name;

            var messageAudits = messagesList
                .Select(m => new MessageAudit
                {
                    Direction = MessageDirection.Outbound,
                    IntegrationType = IntegrationType.AzureServiceBus,
                    Target = queueName,
                    MessageBody = JsonSerializer.Serialize(m),
                    Timestamp = timestamp,
                    MessageType = messageType,
                })
                .ToList();

            var bulkOps = messageAudits
                .Select(WriteModel<MessageAudit> (msg) => new InsertOneModel<MessageAudit>(msg))
                .ToList();

            await mongoContext.MessageAudits.BulkWrite(bulkOps, cancellationToken);
            logger.LogInformation(
                "Stored {Count} outbound messages to MongoDB for {IntegrationType} target {Target}",
                messageAudits.Count,
                IntegrationType.AzureServiceBus,
                queueName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to store outbound messages to MongoDB for {IntegrationType} target {Target}",
                IntegrationType.AzureServiceBus,
                queueName
            );
        }
    }
}
