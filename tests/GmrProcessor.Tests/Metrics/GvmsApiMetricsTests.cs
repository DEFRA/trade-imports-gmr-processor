using System.Diagnostics.Metrics;
using GmrProcessor.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace GmrProcessor.Tests.Metrics;

[Collection("GvmsApiMetrics")]
public class GvmsApiMetricsTests
{
    private readonly GvmsApiMetrics _gvmsApiMetrics;
    private readonly List<CollectedMeasurement<double>> _measurements = new();

    public GvmsApiMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        _gvmsApiMetrics = new GvmsApiMetrics(meterFactory);

        var meterListener = new MeterListener();
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
                _measurements.Add(new CollectedMeasurement<double>(measurement, tags.ToArray()));
            }
        );

        meterListener.Start();
    }

    [Fact]
    public async Task RecordRequest_ShouldRecordMetricsWithSuccess_WhenTaskCompletes()
    {
        const string endpoint = "Hold";
        var taskCompletionSource = new TaskCompletionSource();

        var recordTask = _gvmsApiMetrics.RecordRequest(endpoint, taskCompletionSource.Task);
        taskCompletionSource.SetResult();
        await recordTask;

        _measurements.Should().HaveCount(1);
        _measurements[0].Value.Should().BeGreaterThan(0);

        var tags = _measurements[0].Tags;
        tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", "Hold"));
        tags.Should().Contain(new KeyValuePair<string, object?>("success", true));
        tags.Should().NotContain(tag => tag.Key == "errorType");
    }

    [Fact]
    public async Task RecordRequest_ShouldRecordMetricsWithFailure_WhenTaskThrows()
    {
        const string endpoint = "Hold";
        var taskCompletionSource = new TaskCompletionSource();
        var expectedException = new HttpRequestException("API failure");

        var recordTask = _gvmsApiMetrics.RecordRequest(endpoint, taskCompletionSource.Task);
        taskCompletionSource.SetException(expectedException);

        var thrownException = await Assert.ThrowsAsync<HttpRequestException>(async () => await recordTask);

        _measurements.Should().HaveCount(1);
        _measurements[0].Value.Should().BeGreaterThan(0);

        var tags = _measurements[0].Tags;
        tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", "Hold"));
        tags.Should().Contain(new KeyValuePair<string, object?>("success", false));
        tags.Should().Contain(tag => tag.Key == "errorType");
        var errorTypeTag = tags.FirstOrDefault(t => t.Key == "errorType");
        errorTypeTag.Value.Should().Be("HttpRequestException");

        thrownException.Should().Be(expectedException);
    }

    [Fact]
    public async Task RecordRequest_ShouldRethrowException_AfterRecordingMetrics()
    {
        const string endpoint = "Hold";
        var expectedException = new InvalidOperationException("Test exception");

        var task = Task.FromException(expectedException);

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _gvmsApiMetrics.RecordRequest(endpoint, task)
        );

        thrownException.Should().Be(expectedException);
        _measurements.Should().HaveCount(1);
        _measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("success", false));
        _measurements[0].Tags.Should().Contain(tag => tag.Key == "errorType");
    }

    [Fact]
    public async Task RecordRequest_ShouldRecordDuration_ForSuccessfulRequest()
    {
        const string endpoint = "Hold";

        await _gvmsApiMetrics.RecordRequest(endpoint, Task.Delay(10, TestContext.Current.CancellationToken));

        _measurements.Should().HaveCount(1);
        _measurements[0].Value.Should().BeGreaterThanOrEqualTo(10);
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
