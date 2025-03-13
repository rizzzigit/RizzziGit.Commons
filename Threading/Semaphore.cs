namespace RizzziGit.Commons.Threading;

public static class SemaphoreExtensions
{
    public static void WithSemaphore(this Semaphore semaphore, Action action)
    {
        semaphore.WaitOne();
        try
        {
            action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static T WithSemaphore<T>(this Semaphore semaphore, Func<T> function)
    {
        semaphore.WaitOne();

        try
        {
            return function();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static void WithSemaphore(this SemaphoreSlim semaphoreSlim, Action action)
    {
        semaphoreSlim.Wait(CancellationToken.None);

        try
        {
            action();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public static T WithSemaphore<T>(this SemaphoreSlim semaphoreSlim, Func<T> function)
    {
        semaphoreSlim.Wait(CancellationToken.None);

        try
        {
            return function();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public static async Task WithSemaphore(this SemaphoreSlim semaphoreSlim, Func<Task> function)
    {
        await semaphoreSlim.WaitAsync(CancellationToken.None);

        try
        {
            await function();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public static async Task<T> WithSemaphore<T>(
        this SemaphoreSlim semaphoreSlim,
        Func<Task<T>> function
    )
    {
        await semaphoreSlim.WaitAsync(CancellationToken.None);

        try
        {
            return await function();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}
