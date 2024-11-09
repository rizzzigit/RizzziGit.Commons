namespace RizzziGit.Commons.Logging;

public delegate void LoggerHandler(LogLevel level, string[] scope, string message, ulong timestamp);

public enum LogLevel : byte
{
    Fatal,
    Error,
    Warn,
    Info,
    Debug
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

    private void InternalLog(LogLevel level, string[] scope, string message, ulong timestamp)
    {
        scope = [Name, .. scope];

        Logged?.Invoke(level, scope, message, timestamp);

        foreach (Logger subscriber in SubscriberLoggers)
        {
            subscriber.InternalLog(level, scope, message, timestamp);
        }
    }

    public void Log(LogLevel level, string message)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        InternalLog(level, [], message, (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds());
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);

    public void Info(string message) => Log(LogLevel.Info, message);

    public void Warn(string message) => Log(LogLevel.Warn, message);

    public void Error(string message) => Log(LogLevel.Error, message);

    public void Fatal(string message) => Log(LogLevel.Fatal, message);
}
