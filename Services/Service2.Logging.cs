namespace RizzziGit.Commons.Services;

using Commons.Logging;
using Commons.Utilities;

public abstract partial class Service2<C>
{
    public event LoggerHandler? Logged;

    protected void Log(LogLevel level, string message, string? scope = null) =>
        logger.Log(level, $"{(scope != null ? $"[{scope}] " : "")}{message}");

    protected void Debug(string message, string? scope = null) =>
        Log(LogLevel.Debug, message, scope);

    protected void Info(string message, string? scope = null) => Log(LogLevel.Info, message, scope);

    protected void Warn(string message, string? scope = null) => Log(LogLevel.Warn, message, scope);

    protected void Error(string message, string? scope = null) =>
        Log(LogLevel.Error, message, scope);

    protected void Fatal(string message, string? scope = null) =>
        Log(LogLevel.Fatal, message, scope);

    protected T Warn<T>(T exception, string? scope = null)
        where T : Exception
    {
        Warn(exception.ToPrintable(), scope);

        return exception;
    }

    protected T Error<T>(T exception, string? scope = null)
        where T : Exception
    {
        Error(exception.ToPrintable(), scope);

        return exception;
    }

    protected T Fatal<T>(T exception, string? scope = null)
        where T : Exception
    {
        Fatal(exception.ToPrintable(), scope);

        return exception;
    }
}
