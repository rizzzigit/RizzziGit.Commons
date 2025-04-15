namespace RizzziGit.Commons.Logging;

public delegate void LoggerHandler(Log Log);

public sealed record Log(
    LogLevel Level,
    string[] Scope,
    string Message,
    DateTimeOffset UtcTimestamp,
    string? ThreadName,
    int ThreadId
);

public enum LogLevel : byte
{
    Fatal,
    Error,
    Warn,
    Info,
    Debug,
}

public static class LogLevelExtensions
{
    public static string ToPrintable(this LogLevel level) => level.ToString().PadRight(5, ' ');
}

public sealed class Logger(string name)
{
    private readonly string Name = name;
    private readonly List<Logger> SubscriberLoggers = [];

    public event LoggerHandler? Logged;

    public void Subscribe(Logger logger) => logger.SubscriberLoggers.Add(this);

    public void Subscribe(params Logger[] loggers)
    {
        foreach (Logger logger in loggers)
        {
            Subscribe(logger);
        }
    }

    public void Unsubscribe(params Logger[] loggers)
    {
        foreach (Logger logger in loggers)
        {
            Unsubscribe(logger);
        }
    }

    public void Unsubscribe(Logger logger)
    {
        for (int index = 0; index < logger.SubscriberLoggers.Count; index++)
        {
            if (logger.SubscriberLoggers[index] != this)
            {
                continue;
            }

            logger.SubscriberLoggers.RemoveAt(index--);
        }
    }

    private void InternalLog(
        LogLevel level,
        string[] scope,
        string message,
        DateTimeOffset timestamp,
        object? state = null
    )
    {
        scope = [Name, .. scope];

        Log log = new(
            level,
            scope,
            message,
            timestamp,
            Thread.CurrentThread.Name,
            Environment.CurrentManagedThreadId
        );

        Logged?.Invoke(log);

        foreach (Logger subscriber in SubscriberLoggers)
        {
            subscriber.InternalLog(level, scope, message, timestamp, state);
        }
    }

    public void Log(LogLevel level, string message, string[]? scope = null, object? state = null)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentException($"{level} is not a defined {nameof(LogLevel)}");
        }

        InternalLog(level, scope ?? [], message, DateTimeOffset.UtcNow, state);
    }

    public void Debug(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Debug, message, scope, state);

    public void Info(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Info, message, scope, state);

    public void Warn(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Warn, message, scope, state);

    public void Error(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Error, message, scope, state);

    public void Fatal(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Fatal, message, scope, state);
}
