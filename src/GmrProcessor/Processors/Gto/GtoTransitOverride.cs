using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrProcessor.Processors.Gto;

public class GtoTransitOverride
{
    private GtoTransitOverride(bool isOverrideRequired, string reason)
    {
        IsOverrideRequired = isOverrideRequired;
        Reason = reason;
    }

    public bool IsOverrideRequired { get; }
    public string Reason { get; }

    private static GtoTransitOverride Required(string reason) => new(true, reason);

    private static GtoTransitOverride NotRequired(string reason) => new(false, reason);

    private static readonly string[] s_completeStatusValues = ["rejected", "partially_rejected", "validated"];
    private static readonly string[] s_inspectionRequiredValues = ["required", "inconclusive"];

    public static GtoTransitOverride IsTransitOverrideRequired(ImportPreNotification importPreNotification)
    {
        var inspectionRequired = importPreNotification.PartTwo?.InspectionRequired?.Trim() ?? string.Empty;
        var importStatus = importPreNotification.Status?.Trim() ?? string.Empty;

        var isImportComplete = s_completeStatusValues.Contains(importStatus, StringComparer.OrdinalIgnoreCase);
        var isInspectionRequired = s_inspectionRequiredValues.Contains(
            inspectionRequired,
            StringComparer.OrdinalIgnoreCase
        );

        if (isImportComplete)
        {
            return NotRequired($"Import status is complete : '{importStatus}'");
        }

        return isInspectionRequired
            ? Required("Transit Override Required")
            : NotRequired($"Inspection is not required : '{inspectionRequired}'");
    }
}
