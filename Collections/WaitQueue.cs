namespace RizzziGit.Commons.Collections;

public sealed class WaitQueue<T>(int? capacity = null) : IAsyncEnumerable<T>
{
    private readonly List<T> items = [];
    private readonly List<TaskCompletionSource<TaskCompletionSource<T>>> enqueue = [];
    private readonly List<TaskCompletionSource<T>> dequeue = [];

    public int Count
    {
        get
        {
            lock (this)
            {
                return items.Count;
            }
        }
    }

    public async Task<T> Dequeue(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<T> source = new();

        using CancellationTokenRegistration cancellationTokenRegistration =
            cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));

        lock (this)
        {
            TryGet:

            if (items.TryShift(out T? item))
            {
                if (!source.TrySetResult(item))
                {
                    items.Unshift(item);
                }
            }
            else if (
                enqueue.TryShift(out TaskCompletionSource<TaskCompletionSource<T>>? innerSource)
            )
            {
                if (!innerSource.TrySetResult(source))
                {
                    goto TryGet;
                }
            }
            else
            {
                dequeue.Push(source);
            }
        }

        return await source.Task;
    }

    public async Task Enqueue(T item, CancellationToken cancellationToken = default)
    {
        Enqueue:
        cancellationToken.ThrowIfCancellationRequested();
        {
            TaskCompletionSource<TaskCompletionSource<T>>? source = null;

            lock (this)
            {
                TryGet:
                cancellationToken.ThrowIfCancellationRequested();

                if (dequeue.TryShift(out TaskCompletionSource<T>? innerSource))
                {
                    if (!innerSource.TrySetResult(item))
                    {
                        goto TryGet;
                    }
                }
                else if (!capacity.HasValue || items.Count < capacity.Value)
                {
                    items.Push(item);
                }
                else
                {
                    source = new();
                    enqueue.Add(source);
                }
            }

            using CancellationTokenRegistration cancellationTokenRegistration =
                cancellationToken.Register(() => source?.TrySetCanceled(cancellationToken));

            if (source != null)
            {
                try
                {
                    TaskCompletionSource<T> innerSource = await source.Task;

                    if (!innerSource.TrySetResult(item))
                    {
                        goto Enqueue;
                    }
                }
                catch (OperationCanceledException)
                {
                    goto Enqueue;
                }
            }
        }
    }

    async IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            yield return await Dequeue(cancellationToken);
        }
    }
}
