namespace RizzziGit.Framework.Collections;

public sealed class WaitQueue<T>(int? capacity = null) : IDisposable, IAsyncEnumerable<T>
{
  private readonly int? Capacity = capacity;
  private bool IsDisposed = false;
  private Exception? Exception;
  private readonly Queue<T> Backlog = new();
  private readonly Queue<TaskCompletionSource<TaskCompletionSource<T>>> InsertQueue = new();
  private readonly Queue<TaskCompletionSource<T>> RemoveQueue = new();

  public int BacklogCount => Backlog.Count;
  public int InsertQueueCount => InsertQueue.Count;
  public int RemoveQueueCount => RemoveQueue.Count;
  public int Count => BacklogCount + InsertQueueCount + RemoveQueueCount;

  public void Dispose() => Dispose(null);
  public void Dispose(Exception? exception = null)
  {
    if (IsDisposed)
    {
      throw new ObjectDisposedException(nameof(WaitQueue<T>));
    }

    lock (this)
    {
      IsDisposed = true;
      Exception = exception;

      Backlog.Clear();
      while (InsertQueue.TryDequeue(out TaskCompletionSource<TaskCompletionSource<T>>? insertResult))
      {
        if (insertResult.Task.IsCompleted)
        {
          continue;
        }

        try
        {
          if (Exception != null)
          {
            insertResult.SetException(Exception);
          }
          else
          {
            insertResult.SetCanceled();
          }
        }
        catch { }
      }

      while (RemoveQueue.TryDequeue(out TaskCompletionSource<T>? removeResult))
      {
        if (removeResult.Task.IsCompleted)
        {
          continue;
        }

        try
        {
          if (Exception != null)
          {
            removeResult.SetException(Exception);
          }
          else
          {
            removeResult.SetCanceled();
          }
        }
        catch { }
      }
    }
  }

  public Task<T> Dequeue() => Dequeue(CancellationToken.None);
  public async Task<T> Dequeue(CancellationToken cancellationToken)
  {
    if (IsDisposed)
    {
      throw Exception ?? new ObjectDisposedException(nameof(WaitQueue<T>));
    }

    TaskCompletionSource<T> source = new();

    lock (this)
    {
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
          if (insertResult.Task.IsCanceled)
          {
            continue;
          }

          try
          {
            insertResult.SetResult(source);
            break;
          }
          catch
          {
            continue;
          }
        }
        else
        {
          RemoveQueue.Enqueue(source);
          break;
        }
      }
    }

    return await source.Task.WaitAsync(cancellationToken);
  }

  public Task Enqueue(T item) => Enqueue(item, CancellationToken.None);
  public async Task Enqueue(T item, CancellationToken cancellationToken)
  {
    if (IsDisposed)
    {
      throw Exception ?? new ObjectDisposedException(nameof(WaitQueue<T>));
    }

    TaskCompletionSource<TaskCompletionSource<T>>? insertSource = null;
    lock (this)
    {
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
          if (removeResult.Task.IsCanceled)
          {
            continue;
          }

          try
          {
            removeResult.SetResult(item);
          }
          catch
          {
            continue;
          }

          break;
        }
        else
        {
          insertSource = new();

          CancellationTokenRegistration? insertCancellationTokenRegistration = null;
          insertCancellationTokenRegistration = cancellationToken.Register(() =>
          {
            if (!insertSource.Task.IsCompleted)
            {
              try
              {
                insertSource.SetCanceled(cancellationToken);
              }
              catch { }
            }
            insertCancellationTokenRegistration?.Unregister();
          });

          InsertQueue.Enqueue(insertSource);
          break;
        }
      }
    }

    if (insertSource != null)
    {
      (await insertSource.Task.WaitAsync(cancellationToken)).SetResult(item);
    }
  }

  public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
  {
    if (IsDisposed)
    {
      throw Exception ?? new ObjectDisposedException(nameof(WaitQueue<T>));
    }

    return GetAsyncEnumerator();

    async IAsyncEnumerator<T> GetAsyncEnumerator()
    {
      while (true)
      {
        cancellationToken.ThrowIfCancellationRequested();

        yield return await Dequeue(cancellationToken);
      }
    }
  }
}
