using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrProcessor.Processors.GTO;

public class TransitOverride
{
    private TransitOverride(bool isOverrideRequired, string reason)
    {
        IsOverrideRequired = isOverrideRequired;
        Reason = reason;
    }

    public bool IsOverrideRequired { get; }
    public string Reason { get; }

    private static TransitOverride Required(string reason) => new(true, reason);

    private static TransitOverride NotRequired(string reason) => new(false, reason);

    private static readonly string[] s_completeStatusValues = ["rejected", "valid", "validated"];
    private static readonly string[] s_inspectionRequiredValues = ["required", "inconclusive"];

    public static TransitOverride IsTransitOverrideRequired(ImportPreNotification importPreNotification)
    {
        var inspectionRequired = importPreNotification.PartTwo?.InspectionRequired?.Trim() ?? string.Empty;
        var importStatus = importPreNotification.Status?.Trim() ?? string.Empty;

        var isImportComplete = s_completeStatusValues.Contains(importStatus);
        var isInspectionRequired = s_inspectionRequiredValues.Contains(inspectionRequired);

        if (isImportComplete)
        {
            return NotRequired($"Import status is complete : '{importStatus}'");
        }

        return isInspectionRequired
            ? Required("Transit Override Required")
            : NotRequired($"Inspection is not required : '{inspectionRequired}'");
    }
}
