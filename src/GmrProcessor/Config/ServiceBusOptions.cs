using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public required string ConnectionString { get; init; }
    public required string ImportMatchResultQueueName { get; init; }
}
