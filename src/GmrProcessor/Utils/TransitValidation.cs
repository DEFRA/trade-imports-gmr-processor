using System.Text.RegularExpressions;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrProcessor.Utils;

public class TransitResult
{
    private TransitResult(bool isTransit, string reason, string mrn)
    {
        IsTransit = isTransit;
        Reason = reason;
        Mrn = mrn;
    }

    public bool IsTransit { get; }
    public string? Reason { get; }

    public string? Mrn { get; }

    public static TransitResult ValidTransit(string mrn) => new(true, string.Empty, mrn);

    public static TransitResult NotTransit() => new(false, string.Empty, string.Empty);

    public static TransitResult InvalidTransit(string reason) => new(false, reason, string.Empty);
}

public static class TransitValidation
{
    private const string NewComputerisedTransitSystemReference = "NCTS";

    public static TransitResult IsTransit(ImportPreNotification importPreNotification)
    {
        var provideCtcMrn = importPreNotification.PartOne?.ProvideCtcMrn?.Trim().ToUpperInvariant() ?? string.Empty;

        return provideCtcMrn switch
        {
            "NO" => TransitResult.NotTransit(),
            "YES_ADD_LATER" => TransitResult.InvalidTransit("CTC - Reference not provided yet"),
            "YES" => ValidateTransitReference(importPreNotification),
            _ => TransitResult.InvalidTransit($"Invalid CTC indicator: '{provideCtcMrn}'"),
        };
    }

    private static TransitResult ValidateTransitReference(ImportPreNotification importPreNotification)
    {
        var nctsReference = importPreNotification
            .ExternalReferences?.FirstOrDefault(er =>
                string.Equals(er.System, NewComputerisedTransitSystemReference, StringComparison.OrdinalIgnoreCase)
            )
            ?.Reference;

        if (string.IsNullOrWhiteSpace(nctsReference))
        {
            return TransitResult.InvalidTransit("CTC - Empty NCTS reference");
        }

        nctsReference = nctsReference.Trim().ToUpper();

        return MrnRegex.Value().IsMatch(nctsReference)
            ? TransitResult.ValidTransit(nctsReference)
            : TransitResult.InvalidTransit("CTC - Invalid NCTS reference");
    }
}
