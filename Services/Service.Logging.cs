namespace RizzziGit.Commons.Services;

using Commons.Logging;
using Commons.Utilities;

public abstract partial class Service<C>
{
    public event LoggerHandler? Logged;

    protected void Log(
        LogLevel level,
        string message,
        string[]? scope = null,
        object? state = null
    ) => logger.Log(level, message, scope, state);

    protected void Debug(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Debug, message, scope, state);

    protected void Info(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Info, message, scope, state);

    protected void Warn(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Warn, message, scope, state);

    protected void Error(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Error, message, scope, state);

    protected void Fatal(string message, string[]? scope = null, object? state = null) =>
        Log(LogLevel.Fatal, message, scope, state);

    protected T Warn<T>(T exception, string[]? scope = null, object? state = null)
        where T : Exception
    {
        Warn(exception.ToPrintable(), scope, state);

        return exception;
    }

    protected T Error<T>(T exception, string[]? scope = null, object? state = null)
        where T : Exception
    {
        Error(exception.ToPrintable(), scope, state);

        return exception;
    }

    protected T Fatal<T>(T exception, string[]? scope = null, object? state = null)
        where T : Exception
    {
        Fatal(exception.ToPrintable(), scope, state);

        return exception;
    }
}
