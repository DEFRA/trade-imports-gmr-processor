using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class GtoMatchedGmrsQueueOptions
{
    public const string SectionName = "GtoMatchedGmrsQueueConsumer";

    public required string QueueName { get; init; }
    public int WaitTimeSeconds { get; init; } = 20;
}
