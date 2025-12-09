using System.Net;
using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using WireMock.Client.Extensions;

namespace GmrProcessor.IntegrationTests;

public static class TradeImportsDataApiMockServerExtensions
{
    public static async Task MockImportPreNotificationsByMrn(
        this WireMockClient client,
        string mrn,
        params ImportPreNotificationResponse[] imports
    )
    {
        var mappingBuilder = client.WireMockAdminApi.GetMappingBuilder();
        mappingBuilder.Given(m =>
            m.WithRequest(req => req.WithPath($"/customs-declarations/{mrn}/import-pre-notifications").UsingGet())
                .WithResponse(rsp =>
                    rsp.WithBody(new ImportPreNotificationsResponse(imports).AsJsonString())
                        .WithStatusCode(HttpStatusCode.Created)
                )
        );

        var status = await mappingBuilder.BuildAndPostAsync();
        Assert.NotNull(status.Guid);
    }
}
