using System.Text.Json;

namespace GmrProcessor.Services;

public class StubTradeImportsServiceBus(ILogger<StubTradeImportsServiceBus> logger) : ITradeImportsServiceBus
{
    public Task SendMessagesAsync<T>(
        IEnumerable<T> messages,
        string queueName,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Would send the following to {QueueName}: {Messages}",
            queueName,
            JsonSerializer.Serialize(messages)
        );

        return Task.CompletedTask;
    }
}
