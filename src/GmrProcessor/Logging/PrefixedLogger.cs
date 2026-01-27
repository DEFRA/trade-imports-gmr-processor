namespace GmrProcessor.Logging;

public class PrefixedLogger<T>(ILogger<T> logger, string prefix) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var message = $"{prefix}: {state}";
        logger.Log(logLevel, eventId, message, exception, (_, _) => message);
    }
}
