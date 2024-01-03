namespace RizzziGit.Framework.Tasks;

using Collections;

public sealed class TaskQueue : IDisposable
{
  private readonly WaitQueue<(Func<CancellationToken, Task> callback, CancellationToken cancellationToken, TaskCompletionSource taskCompletionSource)> WaitQueue = new();

  void IDisposable.Dispose() => WaitQueue.Dispose();
  public void Dispose(Exception? exception) => WaitQueue.Dispose(exception);

  public Task RunTask(Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    TaskCompletionSource taskCompletionSource = new();
      await WaitQueue.Enqueue((callback, cancellationToken, taskCompletionSource), cancellationToken);
    await taskCompletionSource.Task;
  }

  public async Task<T> RunTask<T>(Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    TaskCompletionSource<T> taskCompletionSource = new();
    try
    {
      await RunTask(async (cancellationToken) =>
      {
        taskCompletionSource.SetResult(await callback(cancellationToken));
      }, cancellationToken);
    }
    catch (Exception exception)
    {
      taskCompletionSource.SetException(exception);
    }

    return await taskCompletionSource.Task;
  }

  public Task<T> RunTask<T>(Func<T> callback, CancellationToken cancellationToken = default) => RunTask((_) => callback(), cancellationToken);
  public Task<T> RunTask<T>(Func<CancellationToken, T> callback, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return RunTask((cancellationToken) => Task.FromResult(callback(cancellationToken)), cancellationToken);
  }

  public Task RunTask(Action callback, CancellationToken cancellationToken = default) => RunTask((_) => callback(), cancellationToken);
  public Task RunTask(Action<CancellationToken> callback, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return RunTask((cancellationToken) =>
    {
      callback(cancellationToken);
      return Task.CompletedTask;
    }, cancellationToken);
  }

  public async Task Start(CancellationToken cancellationToken = default)
  {
    try
    {
      while (true)
      {
        cancellationToken.ThrowIfCancellationRequested();
        var (callback, remoteCancellationToken, taskCompletionSource) = await WaitQueue.Dequeue(cancellationToken);

        using CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
          remoteCancellationToken,
          cancellationToken
        );

        if (linkedCancellationTokenSource.IsCancellationRequested)
        {
          taskCompletionSource.SetCanceled(linkedCancellationTokenSource.Token);

          continue;
        }

        try
        {
          await callback(linkedCancellationTokenSource.Token);
          taskCompletionSource.SetResult();
        }
        catch (Exception exception)
        {
          taskCompletionSource.SetException(exception);
        }
      }
    }
    catch (Exception exception)
    {
      while (WaitQueue.Count != 0)
      {
        var (_, _, source) = await WaitQueue.Dequeue(cancellationToken);

        source.SetException(exception);
        throw;
      }
    }
  }
}
