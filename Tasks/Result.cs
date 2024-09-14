using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Tasks;

public abstract record Result<T>
{
    public static async Task<Result<T>> Adopt(
        Task<T> task,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return new Success(await task.WaitAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            return new Failure(exception);
        }
    }

    private Result() { }

    public sealed record Success(T Value) : Result<T> { }

    public sealed record Failure(Exception Exception) : Result<T> { }

    public bool IsOk([NotNullWhen(true)] out T? value)
    {
        if (this is Success success)
        {
            value = success.Value!;
            return true;
        }

        value = default;
        return false;
    }

    public bool IsError([NotNullWhen(true)] out Exception? exception)
    {
        if (this is Failure failure)
        {
            exception = failure.Exception;
            return true;
        }

        exception = null;
        return false;
    }

    public bool TryGetValue(
        [NotNullWhen(true)] out T? value,
        [NotNullWhen(false)] out Exception? exception
    )
    {
        if (this is Success success)
        {
            value = success.Value!;
            exception = null;
            return true;
        }
        else if (this is Failure failure)
        {
            value = default;
            exception = failure.Exception;
            return false;
        }

        throw new InvalidOperationException("Invalid state to get value.");
    }
}

public static class SafeTask
{
    public static Task<Result<T>> ToResult<T>(
        Task<T> task,
        CancellationToken cancellationToken = default
    ) => Result<T>.Adopt(task, cancellationToken);
}
