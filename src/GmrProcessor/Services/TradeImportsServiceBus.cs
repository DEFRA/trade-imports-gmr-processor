using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;

namespace GmrProcessor.Services;

[ExcludeFromCodeCoverage]
public class TradeImportsServiceBus(IAzureClientFactory<ServiceBusSender> serviceBusSenderFactory)
    : ITradeImportsServiceBus
{
    public async Task SendMessagesAsync<T>(
        IEnumerable<T> messages,
        string queueName,
        CancellationToken cancellationToken = default
    )
    {
        var sender = serviceBusSenderFactory.CreateClient(queueName);

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

        messageBatch.Dispose();
    }
}
