namespace RizzziGit.Framework.Logging;

public delegate void LoggerHandler(LogLevel level, string scope, string message, ulong timestamp);

public enum LogLevel : byte { Fatal, Error, Warn, Info, Debug }

public sealed class Logger(string name)
{
  private readonly string Name = name;
  private readonly List<Logger> SubscribedLoggers = [];

  public event LoggerHandler? Logged;

  public void Subscribe(Logger logger) => logger.SubscribedLoggers.Add(this);
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
    for (int index = 0; index < logger.SubscribedLoggers.Count; index++)
    {
      if (logger.SubscribedLoggers[index] != this)
      {
        continue;
      }

      logger.SubscribedLoggers.RemoveAt(index--);
    }
  }

  private void InternalLog(LogLevel level, string? scope, string message, ulong timestamp)
  {
    scope = $"{Name}{(scope != null ? $" / {scope}" : "")}";

    Logged?.Invoke(level, scope, message, timestamp);

    for (int index = 0; index < SubscribedLoggers.Count; index++)
    {
      SubscribedLoggers[index].InternalLog(level, scope, message, timestamp);
    }
  }

  public void Log(LogLevel level, string message)
  {
    if (!Enum.IsDefined(typeof(LogLevel), level))
    {
      throw new ArgumentOutOfRangeException(nameof(level));
    }

    InternalLog(level, null, message, (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds());
  }

  public void Debug(string message) => Log(LogLevel.Debug, message);
  public void Info(string message) => Log(LogLevel.Info, message);
  public void Warn(string message) => Log(LogLevel.Warn, message);
  public void Error(string message) => Log(LogLevel.Error, message);
  public void Fatal(string message) => Log(LogLevel.Fatal, message);
}
