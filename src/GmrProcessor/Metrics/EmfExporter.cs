using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace GmrProcessor.Metrics;

[ExcludeFromCodeCoverage]
public static class EmfExporter
{
    private static readonly MeterListener s_meterListener = new();
    private static ILogger _logger = null!;
    private static ILoggerFactory s_loggerFactory = NullLoggerFactory.Instance;
    private static string? s_awsNamespace;

    public static void Init(ILoggerFactory loggerFactory, string? awsNamespace)
    {
        _logger = loggerFactory.CreateLogger(nameof(EmfExporter));
        s_loggerFactory = loggerFactory;
        s_awsNamespace = awsNamespace;

        s_meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name is MetricsConstants.MetricNames.MeterName
                || (instrument.Meter.Name is "System.Net.Http" && instrument.Name is "http.client.request.duration")
            )
                listener.EnableMeasurementEvents(instrument);
        };
        s_meterListener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        s_meterListener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        s_meterListener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        s_meterListener.Start();
    }

    private static void OnMeasurementRecorded<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state
    )
    {
        var tagArray = tags.ToArray();

        _ = Task.Run(() =>
        {
            try
            {
                using var metricsLogger = new MetricsLogger(s_loggerFactory);

                metricsLogger.SetNamespace(s_awsNamespace);
                var dimensionSet = new DimensionSet();

                foreach (var tag in tagArray)
                {
                    if (string.IsNullOrWhiteSpace(tag.Value?.ToString()))
                        continue;

                    dimensionSet.AddDimension(tag.Key, tag.Value!.ToString());
                }

                metricsLogger.SetDimensions(dimensionSet);
                metricsLogger.PutMetric(instrument.Name, Convert.ToDouble(measurement), GetEmfUnit(instrument.Unit));
                metricsLogger.Flush();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push EMF metric");
            }
        });
    }

    private static Unit GetEmfUnit(string? unit)
    {
        return unit switch
        {
            null => Unit.NONE,
            "s" => Unit.SECONDS,
            "ms" => Unit.MILLISECONDS,
            "{request}" => Unit.COUNT,
            _ => Enum.Parse<Unit>(unit),
        };
    }
}
