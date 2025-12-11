namespace GmrProcessor.IntegrationTests.Clients;

/// <summary>
/// Prevents creation of too many connections to the Service Bus Emulator and going over the Connections Quota
/// </summary>
public class ServiceBusFixture : IAsyncLifetime
{
    private const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true";

    private readonly Dictionary<string, ServiceBusClient> _clients = new();

    private ServiceBusClient? _serviceBusClient;

    public ValueTask InitializeAsync()
    {
        _serviceBusClient = new ServiceBusClient(ConnectionString);

        return new ValueTask(Task.CompletedTask);
    }

    public ServiceBusClient GetClient(string queueName)
    {
        var clientKey = $"{queueName}";

        if (!_clients.TryGetValue(clientKey, out var client))
        {
            client = new ServiceBusClient(queueName);
            _clients.Add(clientKey, client);
        }

        return client;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync();
        }

        await Dispose(_serviceBusClient);
    }

    private static async Task Dispose(ServiceBusClient? disposable)
    {
        if (disposable != null)
            await disposable.DisposeAsync();
    }
}
