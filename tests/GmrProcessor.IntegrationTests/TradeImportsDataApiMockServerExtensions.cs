using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Microsoft.AspNetCore.Http;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace GmrProcessor.IntegrationTests;

public static class TradeImportsDataApiMockServerExtensions
{
    public static void MockImportPreNotificationsByMrn(
        this WireMockServer server,
        string mrn,
        params ImportPreNotificationResponse[] imports
    )
    {
        server
            .Given(Request.Create().WithPath($"/customs-declarations/{mrn}/import-pre-notifications").UsingGet())
            .RespondWith(
                Response
                    .Create()
                    .WithBody(new ImportPreNotificationsResponse(imports).AsJsonString())
                    .WithStatusCode(StatusCodes.Status200OK)
            );
    }
}
