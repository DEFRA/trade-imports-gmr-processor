using System.Diagnostics.Metrics;
using GmrProcessor.Metrics;
using GmrProcessor.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Services;

[Collection("GvmsApiMetrics")]
public class GvmsApiClientServiceWithMetricsTests
{
    private readonly Mock<IGvmsApiClientService> _gvmsApiClient = new();
    private readonly GvmsApiClientServiceWithMetrics _service;
    private readonly List<CollectedMeasurement<double>> _measurements = new();

    public GvmsApiClientServiceWithMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();

        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();

        var metrics = new GvmsApiMetrics(meterFactory);
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

        _service = new GvmsApiClientServiceWithMetrics(_gvmsApiClient.Object, metrics);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_ShouldCallGvms()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool holdStatus = true;
        var cancellationToken = CancellationToken.None;

        _gvmsApiClient
            .Setup(s => s.PlaceOrReleaseHold(gmrId, holdStatus, cancellationToken))
            .Returns(Task.CompletedTask);

        await _service.PlaceOrReleaseHold(gmrId, holdStatus, cancellationToken);

        _gvmsApiClient.Verify(s => s.PlaceOrReleaseHold(gmrId, holdStatus, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_ShouldRecordMetricsWithSuccess_WhenSuccessful()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool holdStatus = true;

        _gvmsApiClient
            .Setup(s => s.PlaceOrReleaseHold(gmrId, holdStatus, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None);

        _measurements.Should().HaveCount(1);

        _measurements[0].Value.Should().BeGreaterThan(0); // Duration of request

        var tags = _measurements[0].Tags;
        tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", "Hold"));
        tags.Should().Contain(new KeyValuePair<string, object?>("success", true));
        tags.Should().NotContain(tag => tag.Key == "errorType");
    }

    [Fact]
    public async Task PlaceOrReleaseHold_ShouldRecordMetricsWithFailure_WhenExceptionThrown()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool holdStatus = true;
        var expectedException = new HttpRequestException("API failure");

        _gvmsApiClient
            .Setup(s => s.PlaceOrReleaseHold(gmrId, holdStatus, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var thrownException = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _service.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None)
        );

        _measurements.Should().HaveCount(1);
        var tags = _measurements[0].Tags;
        tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", "Hold"));
        tags.Should().Contain(new KeyValuePair<string, object?>("success", false));
        tags.Should().Contain(tag => tag.Key == "errorType");
        var errorTypeTag = tags.FirstOrDefault(t => t.Key == "errorType");
        errorTypeTag.Value.Should().Be("HttpRequestException");

        thrownException.Should().Be(expectedException);
    }

    private record CollectedMeasurement<T>(T Value, KeyValuePair<string, object?>[] Tags)
        where T : struct;
}
