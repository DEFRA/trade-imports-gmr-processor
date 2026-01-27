using System.Net;
using System.Text;
using System.Text.Json;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Config;
using GmrProcessor.Endpoints;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Processors.MrnChedMatch;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Endpoints;

public class ConsumerEndpointsTests
{
    private const string ExpectedUsername = "user";
    private const string ExpectedPassword = "pass";

    [Fact]
    public async Task Post_WithoutAuth_ReturnsUnauthorized()
    {
        var (app, client, _, _) = await BuildClientAsync();
        await using var _ = app;

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(
            "/consumers/data-events-queue",
            content,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WithMissingResourceTypeHeader_ReturnsBadRequest()
    {
        var (app, client, _, _) = await BuildClientAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("ResourceType header is required");
    }

    [Fact]
    public async Task Post_WithInvalidResourceType_ReturnsBadRequest()
    {
        var (app, client, _, _) = await BuildClientAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        request.Headers.Add("ResourceType", "InvalidType");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Unsupported resourceType");
    }

    [Fact]
    public async Task Post_WithCustomsDeclaration_CallsProcessor_ReturnsAccepted()
    {
        var (app, client, mockCustomsProcessor, _) = await BuildClientAsync();
        await using var _ = app;

        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(CustomsDeclarationFixtures.CustomsDeclarationFixture().Create())
            .Create();

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var body = JsonSerializer.Serialize(customsDeclaration, serializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        request.Headers.Add("ResourceType", ResourceEventResourceTypes.CustomsDeclaration);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        mockCustomsProcessor.Verify(
            p =>
                p.ProcessCustomsDeclaration(
                    It.IsAny<ResourceEvent<CustomsDeclaration>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Post_WithImportPreNotification_CallsProcessor_ReturnsAccepted()
    {
        var (app, client, _, mockImportProcessor) = await BuildClientAsync();
        await using var _ = app;

        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(
                ImportPreNotificationFixtures
                    .ImportPreNotificationFixture(ImportPreNotificationFixtures.GenerateRandomReference())
                    .Create()
            )
            .Create();

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var body = JsonSerializer.Serialize(importPreNotification, serializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        request.Headers.Add("ResourceType", ResourceEventResourceTypes.ImportPreNotification);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        mockImportProcessor.Verify(
            p => p.Process(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Post_WithInvalidJson_ReturnsBadRequest()
    {
        var (app, client, _, _) = await BuildClientAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        request.Headers.Add("ResourceType", ResourceEventResourceTypes.CustomsDeclaration);
        request.Content = new StringContent("{\"invalid\": \"json\"}", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Invalid JSON payload");
    }

    [Fact]
    public async Task Post_WhenProcessorThrows_ReturnsInternalServerError()
    {
        var (app, client, mockCustomsProcessor, _) = await BuildClientAsync();
        await using var _ = app;

        mockCustomsProcessor
            .Setup(p =>
                p.ProcessCustomsDeclaration(
                    It.IsAny<ResourceEvent<CustomsDeclaration>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new Exception("Processing error"));

        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(CustomsDeclarationFixtures.CustomsDeclarationFixture().Create())
            .Create();

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var body = JsonSerializer.Serialize(customsDeclaration, serializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        request.Headers.Add("ResourceType", ResourceEventResourceTypes.CustomsDeclaration);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Post_WithValidAuth_Succeeds()
    {
        var (app, client, _, _) = await BuildClientAsync();
        await using var _ = app;

        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(CustomsDeclarationFixtures.CustomsDeclarationFixture().Create())
            .Create();

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var body = JsonSerializer.Serialize(customsDeclaration, serializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/consumers/data-events-queue");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        request.Headers.Add("ResourceType", ResourceEventResourceTypes.CustomsDeclaration);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private static async Task<(
        WebApplication app,
        HttpClient client,
        Mock<IMrnChedMatchProcessor> mockCustomsProcessor,
        Mock<IGtoImportPreNotificationProcessor> mockImportProcessor
    )> BuildClientAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddRouting();

        builder.Services.AddSingleton(
            Options.Create(
                new FeatureOptions
                {
                    EnableDevEndpoints = true,
                    DevEndpointUsername = ExpectedUsername,
                    DevEndpointPassword = ExpectedPassword,
                }
            )
        );

        var mockCustomsProcessor = new Mock<IMrnChedMatchProcessor>();
        var mockImportProcessor = new Mock<IGtoImportPreNotificationProcessor>();

        mockCustomsProcessor
            .Setup(p =>
                p.ProcessCustomsDeclaration(
                    It.IsAny<ResourceEvent<CustomsDeclaration>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new MrnChedMatchProcessorResult());

        mockImportProcessor
            .Setup(p => p.Process(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GtoImportNotificationProcessorResult());

        builder.Services.AddSingleton(mockCustomsProcessor.Object);
        builder.Services.AddSingleton(mockImportProcessor.Object);

        var app = builder.Build();
        app.MapConsumerEndpoints();

        await app.StartAsync(TestContext.Current.CancellationToken);

        return (app, app.GetTestClient(), mockCustomsProcessor, mockImportProcessor);
    }

    private static string CreateBasicAuthHeader(string username, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return $"Basic {encoded}";
    }
}
