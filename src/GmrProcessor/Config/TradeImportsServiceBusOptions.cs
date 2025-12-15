using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class TradeImportsServiceBusOptions
{
    public const string SectionName = "TradeImportsServiceBus";

    public required string ConnectionString { get; init; }
    public required string EtaQueueName { get; init; }
    public required string ImportMatchResultQueueName { get; init; }
}
