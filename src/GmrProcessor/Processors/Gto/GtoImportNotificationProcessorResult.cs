namespace GmrProcessor.Processors.Gto;

public enum GtoImportNotificationProcessorResult
{
    HoldPlaced,
    HoldReleased,
    NoHoldChange,
    NoMatchedGmrExists,
    SkippedNotATransit,
}
