namespace GmrProcessor.Metrics;

public interface IGvmsApiMetrics
{
    Task RecordRequest(string endpoint, Task func);
}
