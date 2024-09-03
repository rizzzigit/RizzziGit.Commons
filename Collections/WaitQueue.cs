namespace RizzziGit.Commons.Collections;

public sealed class WaitQueue<T>(int? capacity = null) : IDisposable, IAsyncEnumerable<T>
{
  private readonly Queue<T> Backlog = new();
  private readonly Queue<TaskCompletionSource<TaskCompletionSource<T>>> InsertQueue = new();
  private readonly Queue<TaskCompletionSource<T>> RemoveQueue = new();

  private bool IsDisposed = false;
  private Exception? Exception;

  public readonly int? Capacity = capacity;

  public int BacklogCount => Backlog.Count;
  public int InsertQueueCount => InsertQueue.Count;
  public int RemoveQueueCount => RemoveQueue.Count;
  public int Count => BacklogCount + InsertQueueCount + RemoveQueueCount;

  private void ThrowIfDisposed()
  {
    if (Exception != null)
    {
      throw Exception;
    }

    ObjectDisposedException.ThrowIf(IsDisposed, this);
  }

  public void Dispose() => Dispose(null);
  public void Dispose(Exception? exception = null)
  {
    lock (this)
    {
      ThrowIfDisposed();

      IsDisposed = true;
      Exception = exception;
    }
  }

  public Task<T> Dequeue() => Dequeue(CancellationToken.None);
  public Task<T> Dequeue(CancellationToken cancellationToken)
  {
    lock (this)
    {
      if (BacklogCount == 0 && InsertQueueCount == 0 && RemoveQueueCount == 0)
      {
        ThrowIfDisposed();
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
        else if (InsertQueue.TryDequeue(out TaskCompletionSource<TaskCompletionSource<T>>? insertResult))
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
    lock (this)
    {
      ThrowIfDisposed();

      TaskCompletionSource<TaskCompletionSource<T>>? insertSource = null;

      while (true)
      {
        if (cancellationToken.IsCancellationRequested)
        {
          throw new TaskCanceledException("A task was cancelled.", null, cancellationToken);
        }

        if (InsertQueue.Count == 0 && RemoveQueue.Count == 0 && (Capacity == null || Capacity > Backlog.Count))
        {
          Backlog.Enqueue(item);
          break;
        }
        else if (InsertQueue.Count == 0 && RemoveQueue.TryDequeue(out TaskCompletionSource<T>? removeResult))
        {
          if (!removeResult.TrySetResult(item))
          {
            continue;
          }

          break;
        }
        else
        {
          insertSource = new();
          cancellationToken.Register(() => insertSource.TrySetCanceled(cancellationToken));
          InsertQueue.Enqueue(insertSource);
          break;
        }
      }

      return insertSource?.Task.ContinueWith(async (task) => (await task).SetResult(item)).WaitAsync(cancellationToken) ?? Task.CompletedTask;
    }
  }

  public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
  {
    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();

      yield return await Dequeue(cancellationToken);
    }
  }
}
