using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class EtaMatchedGmrsQueueOptions
{
    public const string SectionName = "EtaMatchedGmrsQueueConsumer";

    public required string QueueName { get; init; }
    public int WaitTimeSeconds { get; init; } = 20;
}
