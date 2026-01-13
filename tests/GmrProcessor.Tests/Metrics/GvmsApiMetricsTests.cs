using System.Diagnostics.Metrics;
using GmrProcessor.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace GmrProcessor.Tests.Metrics;

[Collection("GvmsApiMetrics")]
public class GvmsApiMetricsTests
{
    private readonly GvmsApiMetrics _gvmsApiMetrics;

    public GvmsApiMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        _gvmsApiMetrics = new GvmsApiMetrics(meterFactory);
    }

    [Fact]
    public void RecordRequestDuration_ShouldRecordHistogramWithCorrectValue()
    {
        const string endpoint = "Hold";
        const bool success = true;
        var duration = TimeSpan.FromMilliseconds(1234.5);
        var measurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == MetricsConstants.MetricNames.GvmsApiRequestDuration
            )
                listener.EnableMeasurementEvents(instrument);
        };

        meterListener.SetMeasurementEventCallback<double>(
            (_, measurement, tags, _) =>
            {
                measurements.Add(new CollectedMeasurement<double>(measurement, tags.ToArray()));
            }
        );

        meterListener.Start();

        _gvmsApiMetrics.RecordRequestDuration(endpoint, success, duration);

        measurements.Should().HaveCount(1);
        measurements[0].Value.Should().Be(1234.5);
    }

    [Fact]
    public void RecordRequestDuration_ShouldRecordWithCorrectTags_WhenSuccessful()
    {
        const string expectedEndpoint = "Hold";
        const bool expectedSuccess = true;
        var duration = TimeSpan.FromMilliseconds(100);
        var measurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == MetricsConstants.MetricNames.GvmsApiRequestDuration
            )
                listener.EnableMeasurementEvents(instrument);
        };

        meterListener.SetMeasurementEventCallback<double>(
            (_, measurement, tags, _) =>
            {
                measurements.Add(new CollectedMeasurement<double>(measurement, tags.ToArray()));
            }
        );

        meterListener.Start();

        _gvmsApiMetrics.RecordRequestDuration(expectedEndpoint, expectedSuccess, duration);

        measurements.Should().HaveCount(1);
        var tags = measurements[0].Tags;
        tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", expectedEndpoint));
        tags.Should().Contain(new KeyValuePair<string, object?>("success", expectedSuccess));
    }

    [Fact]
    public void RecordRequestDuration_ShouldIncludeErrorType_WhenFailureWithErrorType()
    {
        const string endpoint = "Hold";
        const bool success = false;
        const string errorType = "HttpRequestException";
        var duration = TimeSpan.FromMilliseconds(100);
        var measurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == MetricsConstants.MetricNames.GvmsApiRequestDuration
            )
                listener.EnableMeasurementEvents(instrument);
        };

        meterListener.SetMeasurementEventCallback<double>(
            (_, measurement, tags, _) =>
            {
                measurements.Add(new CollectedMeasurement<double>(measurement, tags.ToArray()));
            }
        );

        meterListener.Start();

        _gvmsApiMetrics.RecordRequestDuration(endpoint, success, duration, errorType);

        measurements.Should().HaveCount(1);
        var tags = measurements[0].Tags;
        tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", endpoint));
        tags.Should().Contain(new KeyValuePair<string, object?>("success", success));
        tags.Should().Contain(new KeyValuePair<string, object?>("errorType", errorType));
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
