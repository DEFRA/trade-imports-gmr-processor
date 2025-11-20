namespace GmrProcessor.IntegrationTests;

public static class AsyncWaiter
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

    public static async Task<T?> WaitForAsync<T>(
        Func<Task<T?>> condition,
        CancellationToken cancellationToken = default
    )
    {
        var deadline = DateTime.UtcNow + s_timeout;

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await condition().ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < s_pollInterval ? remaining : s_pollInterval, cancellationToken)
                .ConfigureAwait(false);
        }

        return default;
    }
}
