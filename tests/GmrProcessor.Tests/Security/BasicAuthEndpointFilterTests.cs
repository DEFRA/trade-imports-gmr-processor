using System.Net;
using System.Text;
using GmrProcessor.Config;
using GmrProcessor.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Tests.Security;

public class BasicAuthEndpointFilterTests
{
    private const string ExpectedUsername = "user";
    private const string ExpectedPassword = "pass";

    [Fact]
    public async Task InvokeAsync_WhenAuthorizationMissing_ReturnsUnauthorized()
    {
        var (app, client) = await BuildClientAsync(configureCredentials: true);
        await using var _ = app;

        var response = await client.GetAsync("/secured", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorizationInvalid_ReturnsUnauthorized()
    {
        var (app, client) = await BuildClientAsync(configureCredentials: true);
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secured");
        request.Headers.Add("Authorization", CreateBasicAuthHeader("wrong", "creds"));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Bearer token")]
    [InlineData("Basic")]
    [InlineData("Basic ")]
    [InlineData("Basic not-base64")]
    [InlineData("Basic dXNlcnBhc3M=")]
    public async Task InvokeAsync_WhenAuthorizationMalformed_ReturnsUnauthorized(string authorization)
    {
        var (app, client) = await BuildClientAsync(configureCredentials: true);
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secured");
        request.Headers.Add("Authorization", authorization);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorizationSchemeNotBasic_ReturnsUnauthorized()
    {
        var (app, client) = await BuildClientAsync(configureCredentials: true);
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secured");
        request.Headers.Add("Authorization", "Bearer token");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorizationValid_ReturnsOk()
    {
        var (app, client) = await BuildClientAsync(configureCredentials: true);
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secured");
        request.Headers.Add("Authorization", CreateBasicAuthHeader(ExpectedUsername, ExpectedPassword));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthNotConfigured_ReturnsProblem()
    {
        var (app, client) = await BuildClientAsync(configureCredentials: false);
        await using var _ = app;

        var response = await client.GetAsync("/secured", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    private static async Task<(WebApplication app, HttpClient client)> BuildClientAsync(bool configureCredentials)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddRouting();

        if (configureCredentials)
        {
            builder.Services.AddSingleton(
                Options.Create(
                    new FeatureOptions
                    {
                        DevEndpointUsername = ExpectedUsername,
                        DevEndpointPassword = ExpectedPassword,
                    }
                )
            );
        }
        else
        {
            builder.Services.AddSingleton(Options.Create(new FeatureOptions()));
        }

        var app = builder.Build();
        app.MapGet("/secured", () => Results.Ok()).AddEndpointFilter<BasicAuthEndpointFilter>();
        await app.StartAsync(TestContext.Current.CancellationToken);

        return (app, app.GetTestClient());
    }

    private static string CreateBasicAuthHeader(string username, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return $"Basic {encoded}";
    }
}
