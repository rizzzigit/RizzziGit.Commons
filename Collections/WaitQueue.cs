using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Collections;

public sealed class WaitQueue<T>(int? capacity = null)
    : IDisposable,
        IAsyncDisposable,
        IAsyncEnumerable<T>
{
    private readonly ConcurrentQueue<T> Backlog = new();
    private readonly Queue<TaskCompletionSource<TaskCompletionSource<T>>> InsertQueue = new();
    private readonly Queue<TaskCompletionSource<T>> RemoveQueue = new();

    private bool isDone = false;
    private ExceptionDispatchInfo? exception;

    public bool IsDone => isDone;

    public int? Capacity => capacity;

    public int BacklogCount => Backlog.Count;
    public int InsertQueueCount => InsertQueue.Count;
    public int RemoveQueueCount => RemoveQueue.Count;
    public int Count => BacklogCount + InsertQueueCount;

    public void Dispose(Exception? exception = null)
    {
        lock (this)
        {
            if (isDone)
            {
                this.exception?.Throw();
                throw new InvalidOperationException("Queue is already done.");
            }

            isDone = true;

            if (exception != null)
            {
                this.exception = ExceptionDispatchInfo.Capture(exception);

                while (RemoveQueue.TryDequeue(out var task))
                {
                    task.TrySetException(exception);
                }
            }
        }
    }

    public Task<T> Dequeue() => Dequeue(CancellationToken.None);

    public Task<T> Dequeue(CancellationToken cancellationToken)
    {
        lock (this)
        {
            if (BacklogCount == 0 && InsertQueueCount == 0 && isDone)
            {
                exception?.Throw();
                throw new InvalidOperationException("Queue is done.");
            }

            TaskCompletionSource<T> source = new();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled(cancellationToken);
                    break;
                }

                if (Backlog.TryDequeue(out T? backlogResult))
                {
                    source.SetResult(backlogResult);
                    break;
                }
                else if (
                    InsertQueue.TryDequeue(
                        out TaskCompletionSource<TaskCompletionSource<T>>? insertResult
                    )
                )
                {
                    if (!insertResult.TrySetResult(source))
                    {
                        continue;
                    }

                    break;
                }
                else
                {
                    RemoveQueue.Enqueue(source);
                    break;
                }
            }

            return source.Task.WaitAsync(cancellationToken);
        }
    }

    public Task Enqueue(T item) => Enqueue(item, CancellationToken.None);

    public Task Enqueue(T item, CancellationToken cancellationToken)
    {
        if (isDone)
        {
            exception?.Throw();
            throw new InvalidOperationException("Queue is done.");
        }

        TaskCompletionSource<TaskCompletionSource<T>>? insertSource = null;

        lock (this)
        {
            CancellationTokenRegistration? registration = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (
                    InsertQueue.Count == 0
                    && RemoveQueue.Count == 0
                    && (capacity == null || capacity > Backlog.Count)
                )
                {
                    Backlog.Enqueue(item);
                    break;
                }

                if (
                    InsertQueue.Count == 0
                    && RemoveQueue.TryDequeue(out TaskCompletionSource<T>? removeResult)
                )
                {
                    if (!removeResult.TrySetResult(item))
                    {
                        continue;
                    }

                    break;
                }

                insertSource = new();
                registration = cancellationToken.Register(
                    () => insertSource.TrySetCanceled(cancellationToken)
                );

                InsertQueue.Enqueue(insertSource);
                break;
            }
        }

        return insertSource
                ?.Task.ContinueWith(async (task) => (await task).SetResult(item))
                .WaitAsync(cancellationToken) ?? Task.CompletedTask;
    }

    async IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            Task<T> item;

            lock (this)
            {
                if (BacklogCount == 0 && InsertQueueCount == 0 && isDone)
                {
                    exception?.Throw();
                    yield break;
                }

                TaskCompletionSource<T> source = new();

                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        source.SetCanceled(cancellationToken);
                        break;
                    }

                    if (Backlog.TryDequeue(out T? backlogResult))
                    {
                        source.SetResult(backlogResult);
                        break;
                    }
                    else if (
                        InsertQueue.TryDequeue(
                            out TaskCompletionSource<TaskCompletionSource<T>>? insertResult
                        )
                    )
                    {
                        if (!insertResult.TrySetResult(source))
                        {
                            continue;
                        }

                        break;
                    }
                    else
                    {
                        RemoveQueue.Enqueue(source);
                        break;
                    }
                }

                item = source.Task.WaitAsync(cancellationToken);
            }

            yield return await item;
        }
    }

    void IDisposable.Dispose() => Dispose();

    async ValueTask IAsyncDisposable.DisposeAsync() => await Task.Factory.StartNew(() => Dispose());
}
