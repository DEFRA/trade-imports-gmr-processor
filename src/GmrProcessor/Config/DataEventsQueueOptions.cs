using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class DataEventsQueueConsumerOptions
{
    public const string SectionName = "DataEventsQueueConsumer";

    public required string QueueName { get; init; }
    public int WaitTimeSeconds { get; init; } = 20;
}
