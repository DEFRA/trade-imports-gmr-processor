using System.Diagnostics.Metrics;
using GmrProcessor.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace GmrProcessor.Tests.Metrics;

public class ConsumerMetricsTests
{
    private readonly ConsumerMetrics _consumerMetrics;

    public ConsumerMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        _consumerMetrics = new ConsumerMetrics(meterFactory);
    }

    [Fact]
    public void RecordExecutionDuration_ShouldRecordHistogram()
    {
        const string expectedQueueName = "test-queue";
        const bool expectedSuccess = true;
        var expectedDuration = TimeSpan.FromMilliseconds(12345);
        var measurements = new List<CollectedMeasurement<double>>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (
                instrument.Meter.Name == MetricsConstants.MetricNames.MeterName
                && instrument.Name == "queue.process.message.duration"
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

        _consumerMetrics.RecordProcessDuration(expectedQueueName, expectedSuccess, expectedDuration);

        measurements.Should().HaveCount(1);
        var measurement = measurements[0];
        measurement.Value.Should().Be(12345);
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("queueName", expectedQueueName));
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("success", expectedSuccess));
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
