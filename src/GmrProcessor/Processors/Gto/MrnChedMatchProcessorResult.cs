namespace GmrProcessor.Processors.Gto;

public enum MrnChedMatchProcessorResult
{
    MatchCreated,
    SkippedNoMrn,
    SkippedInvalidMrn,
    SkippedNoChedReferences,
}
