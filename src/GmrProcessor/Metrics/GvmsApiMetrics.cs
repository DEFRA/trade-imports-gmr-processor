using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace GmrProcessor.Metrics;

public class GvmsApiMetrics : IGvmsApiMetrics
{
    private readonly Histogram<double> _requestDuration;

    public GvmsApiMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MetricsConstants.MetricNames.MeterName);

        _requestDuration = meter.CreateHistogram<double>(
            MetricsConstants.MetricNames.GvmsApiRequestDuration,
            nameof(Unit.MILLISECONDS),
            "Duration of GVMS API requests"
        );
    }

    private void RecordRequestDuration(string endpoint, bool success, TimeSpan duration, string? errorType = null)
    {
        var tagList = new TagList { { "endpoint", endpoint }, { "success", success } };

        if (!success && errorType != null)
        {
            tagList.Add("errorType", errorType);
        }

        _requestDuration.Record(duration.TotalMilliseconds, tagList);
    }

    public async Task RecordRequest(string endpoint, Task func)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorType = null;

        try
        {
            await func;
            success = true;
        }
        catch (Exception ex)
        {
            errorType = ex.GetType().Name;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            RecordRequestDuration(endpoint, success, stopwatch.Elapsed, errorType);
        }
    }
}
