using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Config;

[ExcludeFromCodeCoverage]
public class ImportMatchedGmrsQueueOptions : QueueOptions
{
    public const string SectionName = "ImportMatchedGmrsQueueConsumer";
}
