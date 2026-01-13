using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Metrics;

[ExcludeFromCodeCoverage]
public static class MetricsConstants
{
    public static class MetricNames
    {
        public const string MeterName = "Defra.Trade.GmrProcessor";
        public const string GvmsApiRequestDuration = "gvms.api.request.duration";
    }
}
