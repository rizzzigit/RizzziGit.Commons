using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Result;

public interface IResult<T, E>;

public interface ISuccess<T, E> : IResult<T, E>
{
    public T Value { get; }
}

public interface IFailure<T, E> : IResult<T, E>;

public interface IResult<T> : IResult<T, Exception>;

public interface ISuccess<T> : IResult<T, Exception>, ISuccess<T, Exception>;

public interface IFailure<T> : IResult<T, Exception>, IFailure<T, Exception>;

public interface IResult : IResult<object>;

public interface ISuccess : IResult, ISuccess<object>;

public interface IFailure : IResult, IFailure<Exception>;

public abstract record Result<T, E> : IResult<T, E>
{
    public static Success FromSuccess(T value) => new(value);

    public static Failure FromFailure(E exception) => new(exception);

    public sealed record Success(T Value) : Result<T, E>, ISuccess<T, E>;

    public sealed record Failure(E Error) : Result<T, E>, IFailure<T, E>;

    public bool IsSuccess([NotNullWhen(true)] out T value)
    {
        if (this is Success(T result))
        {
            value = result;
            return true;
        }

        value = default!;
        return false;
    }

    public bool IsFailure([NotNullWhen(true)] out E error)
    {
        if (this is Failure(E result))
        {
            error = result;
            return true;
        }

        error = default!;
        return false;
    }
}

public abstract record Result<T> : Result<T, Exception>, IResult<T>
{
    public static async ValueTask<Result<T>> Wrap(Func<ValueTask<T>> func)
    {
        try
        {
            return new Success(await func());
        }
        catch (Exception exception)
        {
            return new Failure(exception);
        }
    }

    public static Result<T> Wrap(Func<T> action)
    {
        try
        {
            return new Success(action());
        }
        catch (Exception exception)
        {
            return new Failure(exception);
        }
    }

    public static new Success FromSuccess(T value) => new(value);

    public static new Failure FromFailure(Exception exception) => new(exception);

    public new sealed record Success(T Value) : Result<T>, ISuccess<T>;

    public new sealed record Failure(Exception Error) : Result<T>, IFailure<T>;
}

public abstract record Result : Result<object, Exception>
{
    public static async ValueTask<Result> Wrap(Func<ValueTask> func)
    {
        try
        {
            await func();
            return new Success();
        }
        catch (Exception exception)
        {
            return new Failure(exception);
        }
    }

    public static Result Wrap(Action action)
    {
        try
        {
            action();
            return new Success();
        }
        catch (Exception exception)
        {
            return new Failure(exception);
        }
    }

    public static Success FromSuccess() => new();

    public static new Failure FromFailure(Exception exception) => new(exception);

    public new sealed record Success() : Result, ISuccess
    {
        public object Value => throw new NotImplementedException();
    }

    public new sealed record Failure(Exception Error) : Result, IFailure;
}
