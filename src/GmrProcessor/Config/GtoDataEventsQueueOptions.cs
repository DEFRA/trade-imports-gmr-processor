using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class GtoDataEventsQueueConsumerOptions
{
    public const string SectionName = "GtoDataEventsQueueConsumer";

    public required string QueueName { get; init; }
    public int WaitTimeSeconds { get; init; } = 20;
}
