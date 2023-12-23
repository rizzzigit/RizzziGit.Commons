namespace RizzziGit.Framework.Logging;

public delegate void LoggerHandler(LogLevel level, string scope, string message, ulong timestamp);

public enum LogLevel : byte
{
  Verbose = 5,
  Info = 4,
  Warn = 3,
  Error = 2,
  Fatal = 1
}

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
}
