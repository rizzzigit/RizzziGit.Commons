namespace RizzziGit.Commons.Threading;

public static class SemaphoreExtensions
{
    extension (Semaphore semaphore)
    {
        public void WithSemaphore(Action action)
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

        public T WithSemaphore<T>(Func<T> function)
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
    }

    extension (SemaphoreSlim semaphoreSlim)
    {
        public void WithSemaphore(
            Action action,
            CancellationToken cancellationToken = default
        )
        {
            semaphoreSlim.Wait(cancellationToken);

            try
            {
                action();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public T WithSemaphore<T>(
            Func<T> function,
            CancellationToken cancellationToken = default
        )
        {
            semaphoreSlim.Wait(cancellationToken);

            try
            {
                return function();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task WithSemaphore(
            Func<Task> function,
            CancellationToken cancellationToken = default
        )
        {
            await semaphoreSlim.WaitAsync(cancellationToken);

            try
            {
                await function();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task<T> WithSemaphore<T>(
            Func<Task<T>> function,
            CancellationToken cancellationToken = default
        )
        {
            await semaphoreSlim.WaitAsync(cancellationToken);

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
}
