using System.Net;
using System.Text;
using System.Text.Json;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Data.Auditing;
using GmrProcessor.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace GmrProcessor.Tests.Endpoints;

public class MessageEndpointTests
{
    private const string ExpectedUsername = "user";
    private const string ExpectedPassword = "pass";

    [Fact]
    public async Task GetMessages_WithoutAuth_ReturnsUnauthorized()
    {
        var (app, client, _) = await BuildClientAsync();
        await using var _ = app;

        var response = await client.GetAsync("/messages?messageType=Test", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMessages_WithoutMessageType_ReturnsBadRequest()
    {
        var (app, client, _) = await BuildClientAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/messages");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_WithValidAuth_ReturnsMessages()
    {
        var expectedMessages = new List<MessageAudit>
        {
            new()
            {
                Id = "1",
                Direction = MessageDirection.Outbound,
                IntegrationType = IntegrationType.GvmsApi,
                Target = "test",
                MessageBody = "{}",
                Timestamp = DateTime.UtcNow,
                MessageType = "TestMessage",
            },
        };

        var (app, client, mockRepo) = await BuildClientAsync(expectedMessages);
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/messages?messageType=TestMessage");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var messages = JsonSerializer.Deserialize<List<MessageAudit>>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        messages.Should().NotBeNull();
        messages.Should().HaveCount(1);
        messages[0].MessageType.Should().Be("TestMessage");

        mockRepo.Verify(
            r =>
                r.GetByMessageTypeAsync(
                    "TestMessage",
                    It.Is<DateTime>(dt => dt <= DateTime.UtcNow && dt >= DateTime.UtcNow.AddMinutes(-16)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetMessages_WhenRepositoryThrows_ReturnsInternalServerError()
    {
        var (app, client, mockRepo) = await BuildClientAsync(throwException: true);
        await using var _ = app;

        mockRepo
            .Setup(r =>
                r.GetByMessageTypeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new Exception("Database error"));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/messages?messageType=Test");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetMessages_WithEmptyMessageType_ReturnsBadRequest()
    {
        var (app, client, _) = await BuildClientAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/messages?messageType=");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_ReturnsEmptyArrayWhenNoResults()
    {
        var (app, client, _) = await BuildClientAsync(new List<MessageAudit>());
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/messages?messageType=NonExistent");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var messages = JsonSerializer.Deserialize<List<MessageAudit>>(content);

        messages.Should().NotBeNull();
        messages.Should().BeEmpty();
    }

    private static async Task<(
        WebApplication app,
        HttpClient client,
        Mock<IMessageAuditRepository> mockRepo
    )> BuildClientAsync(List<MessageAudit>? messages = null, bool throwException = false)
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

        var mockRepo = new Mock<IMessageAuditRepository>();

        if (!throwException)
        {
            mockRepo
                .Setup(r =>
                    r.GetByMessageTypeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(messages ?? []);
        }

        builder.Services.AddSingleton(mockRepo.Object);

        var app = builder.Build();
        app.MapMessageEndpoints();

        await app.StartAsync(TestContext.Current.CancellationToken);

        return (app, app.GetTestClient(), mockRepo);
    }

    private static string CreateBasicAuthHeader(string username, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return $"Basic {encoded}";
    }
}
