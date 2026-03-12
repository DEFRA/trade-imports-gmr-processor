using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Microsoft.Extensions.Http.Resilience;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class GmrProcessorGvmsApiOptions : GvmsApiOptions
{
    public const string SectionName = "GvmsApi";

    private const int GvmsApiRateLimit = 3;

    public int DeployedContainerCount { get; set; } = 3;
    public int QueueLimit { get; set; } = 10;

    public HttpCircuitBreakerStrategyOptions CircuitBreaker { get; } = new();

    public HttpRetryStrategyOptions Retry { get; } = new() { MaxRetryAttempts = 3, Delay = TimeSpan.FromSeconds(1) };

    public HttpTimeoutStrategyOptions Timeout { get; } = new() { Timeout = TimeSpan.FromSeconds(30) };

    public TokenBucketRateLimiterOptions GetRateLimiterOptions()
    {
        var tokensPerPeriod = GvmsApiRateLimit / DeployedContainerCount;

        return new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = tokensPerPeriod,
            QueueLimit = QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        };
    }
}
