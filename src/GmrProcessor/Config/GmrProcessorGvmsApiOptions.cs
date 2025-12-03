using System.Diagnostics.CodeAnalysis;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Microsoft.Extensions.Http.Resilience;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class GmrProcessorGvmsApiOptions : GvmsApiOptions
{
    public const string SectionName = "GvmsApi";

    public HttpCircuitBreakerStrategyOptions CircuitBreaker { get; } = new();
    public HttpRetryStrategyOptions Retry { get; } = new() { MaxRetryAttempts = 3 };
    public HttpTimeoutStrategyOptions Timeout { get; } = new() { Timeout = TimeSpan.FromSeconds(30) };
}
