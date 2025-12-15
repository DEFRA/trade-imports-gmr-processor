namespace GmrProcessor.Services;

public interface ITradeImportsServiceBus
{
    Task SendMessagesAsync<T>(IEnumerable<T> messages, string queueName, CancellationToken cancellationToken = default);
}
