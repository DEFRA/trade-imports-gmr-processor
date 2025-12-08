using WireMock.Server;
using WireMock.Settings;

namespace GmrProcessor.IntegrationTests;

#pragma warning disable S3881
public class WireMockContext : IDisposable
#pragma warning restore S3881
{
    public WireMockServer TradeImportsDataApiMockServer { get; } =
        WireMockServer.Start(new WireMockServerSettings { Urls = ["http://localhost:9090"] });

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        TradeImportsDataApiMockServer.Stop();
        TradeImportsDataApiMockServer.Dispose();
    }
}
