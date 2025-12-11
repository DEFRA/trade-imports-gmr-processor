namespace GmrProcessor.Services;

public interface IServiceBusSenderService
{
    Task SendMessagesAsync<T>(IEnumerable<T> messages, string queueName, CancellationToken cancellationToken = default);
}
