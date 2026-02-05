using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class TradeImportsServiceBusOptions
{
    public const string SectionName = "TradeImportsServiceBus";
    public required ServiceBusQueue Eta { get; init; }
    public required ServiceBusQueue ImportMatchResult { get; init; }
}

[ExcludeFromCodeCoverage]
public class ServiceBusQueue
{
    public required string ConnectionString { get; init; }
    public required string QueueName { get; init; }
}
