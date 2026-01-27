namespace GmrProcessor.Processors.MrnChedMatch;

public enum MrnChedMatchProcessorResult
{
    MatchCreated,
    SkippedNoMrn,
    SkippedInvalidMrn,
    SkippedNoChedReferences,
}
