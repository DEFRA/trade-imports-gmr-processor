using GmrProcessor.Config;
using GmrProcessor.Extensions;
using GmrProcessor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GmrProcessor.Tests.Extensions;

public class ServiceCollectionExtensionsTradeImportsMessagingTests
{
    [Fact]
    public void AddTradeImportsMessaging_WhenDisabled_RegistersStubTradeImportsServiceBus()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ENABLE_TRADE_IMPORTS_MESSAGING"] = "false" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddTradeImportsMessaging(configuration);

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITradeImportsServiceBus>().Should().BeOfType<StubTradeImportsServiceBus>();
    }

    [Fact]
    public void AddTradeImportsMessaging_WhenEnabled_RegistersTradeImportsServiceBus()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ENABLE_TRADE_IMPORTS_MESSAGING"] = "true",
                    ["TradeImportsServiceBus:Eta:ConnectionString"] =
                        "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE",
                    ["TradeImportsServiceBus:Eta:QueueName"] = "eta-queue",
                    ["TradeImportsServiceBus:ImportMatchResult:ConnectionString"] =
                        "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE",
                    ["TradeImportsServiceBus:ImportMatchResult:QueueName"] = "import-match-result-queue",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<CdpOptions>().Bind(configuration);

        services.AddTradeImportsMessaging(configuration);

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITradeImportsServiceBus>().Should().BeOfType<TradeImportsServiceBus>();
    }
}
