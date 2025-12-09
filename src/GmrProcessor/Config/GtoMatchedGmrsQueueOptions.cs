using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class GtoMatchedGmrsQueueOptions : QueueOptions
{
    public const string SectionName = "GtoMatchedGmrsQueueConsumer";
}
