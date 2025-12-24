using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace GmrProcessor.Metrics;

public class ConsumerMetrics
{
    private readonly Histogram<double> _jobExecutionDuration;

    public ConsumerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MetricsConstants.MetricNames.MeterName);

        _jobExecutionDuration = meter.CreateHistogram<double>(
            "queue.process.message.duration",
            nameof(Unit.MILLISECONDS),
            "Duration of processing a message from a queue"
        );
    }

    public void RecordProcessDuration(string queueName, bool success, TimeSpan duration)
    {
        var tagList = new TagList { { "queueName", queueName }, { "success", success } };
        _jobExecutionDuration.Record(duration.TotalMilliseconds, tagList);
    }
}
