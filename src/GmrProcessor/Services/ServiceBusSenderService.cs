using System.Text.Json;
using Azure.Messaging.ServiceBus;
using GmrProcessor.Config;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Services;

public class ServiceBusSenderService : IServiceBusSenderService, IAsyncDisposable
{
    private readonly ServiceBusClient _serviceBusClient;

    public ServiceBusSenderService(IOptions<ServiceBusOptions> options)
    {
        var serviceBusOptions = options.Value;
        _serviceBusClient = new ServiceBusClient(serviceBusOptions.ConnectionString);
    }

    public async Task SendMessagesAsync<T>(
        IEnumerable<T> messages,
        string queueName,
        CancellationToken cancellationToken = default
    )
    {
        var sender = _serviceBusClient.CreateSender(queueName);
        var messageBatch = await sender.CreateMessageBatchAsync(cancellationToken);

        foreach (var message in messages)
        {
            var json = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(json);

            if (!messageBatch.TryAddMessage(serviceBusMessage))
            {
                await sender.SendMessagesAsync(messageBatch, cancellationToken);
                messageBatch.Dispose();
                messageBatch = await sender.CreateMessageBatchAsync(cancellationToken);

                if (!messageBatch.TryAddMessage(serviceBusMessage))
                {
                    throw new InvalidOperationException($"Message is too large to fit in a batch");
                }
            }
        }

        if (messageBatch.Count > 0)
        {
            await sender.SendMessagesAsync(messageBatch, cancellationToken);
        }

        await sender.DisposeAsync();
        messageBatch.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceBusClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
