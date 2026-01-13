using System.Diagnostics;
using GmrProcessor.Metrics;

namespace GmrProcessor.Services;

public class GvmsApiClientServiceWithMetrics(IGvmsApiClientService gvmsApiClient, GvmsApiMetrics metrics)
    : IGvmsApiClientService
{
    public async Task PlaceOrReleaseHold(string gmrId, bool holdStatus, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorType = null;

        try
        {
            await gvmsApiClient.PlaceOrReleaseHold(gmrId, holdStatus, cancellationToken);
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
            metrics.RecordRequestDuration("Hold", success, stopwatch.Elapsed, errorType);
        }
    }
}
