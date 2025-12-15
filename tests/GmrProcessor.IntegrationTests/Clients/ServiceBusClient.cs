using Azure.Messaging.ServiceBus;

namespace GmrProcessor.IntegrationTests.Clients;

public class ServiceBusClient : IAsyncDisposable
{
    private const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true";

    private readonly Azure.Messaging.ServiceBus.ServiceBusClient _serviceBusClient;
    private readonly ServiceBusReceiver? _serviceBusReceiver;

    public ServiceBusReceiver Receiver =>
        _serviceBusReceiver ?? throw new InvalidOperationException("Service Bus Receiver not initialized");

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        var receiver =
            _serviceBusReceiver ?? throw new InvalidOperationException("Service Bus Receiver not initialized");

        // Keep receiving until no more messages are available
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(1), cancellationToken);
            if (messages.Count == 0)
                break;
        }
    }

    public ServiceBusClient(string queueName)
    {
        _serviceBusClient = new Azure.Messaging.ServiceBus.ServiceBusClient(ConnectionString);
        _serviceBusReceiver = _serviceBusClient.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions() { PrefetchCount = 10, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete }
        );
    }

    public async ValueTask DisposeAsync()
    {
        await Dispose(_serviceBusReceiver);
        await Dispose(_serviceBusClient);
        GC.SuppressFinalize(this);
    }

    private static async Task Dispose(IAsyncDisposable? disposable)
    {
        if (disposable != null)
            await disposable.DisposeAsync();
    }
}
