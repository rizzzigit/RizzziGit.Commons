namespace RizzziGit.Framework.Tasks;

using Collections;

internal class TaskQueue : IDisposable
{
  private readonly WaitQueue<(Func<CancellationToken, Task> callback, CancellationToken cancellationToken, TaskCompletionSource taskCompletionSource)> WaitQueue = new();

  void IDisposable.Dispose() => WaitQueue.Dispose();
  public void Dispose(Exception? exception) => WaitQueue.Dispose(exception);

  public async Task RunTask(Func<CancellationToken, Task> callback, CancellationToken? cancellationToken = null)
  {
    cancellationToken?.ThrowIfCancellationRequested();

    TaskCompletionSource taskCompletionSource = new();
    await WaitQueue.Enqueue((callback, cancellationToken ?? CancellationToken.None, taskCompletionSource), cancellationToken ?? CancellationToken.None);
    await taskCompletionSource.Task;
  }

  public async Task<T> RunTask<T>(Func<CancellationToken, Task<T>> callback, CancellationToken? cancellationToken = null)
  {
    cancellationToken?.ThrowIfCancellationRequested();

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

  public Task<T> RunTask<T>(Func<T> callback, CancellationToken? cancellationToken = null) => RunTask((_) => callback(), cancellationToken);
  public Task<T> RunTask<T>(Func<CancellationToken, T> callback, CancellationToken? cancellationToken = null)
  {
    cancellationToken?.ThrowIfCancellationRequested();

    return RunTask((cancellationToken) => Task.FromResult(callback(cancellationToken)), cancellationToken);
  }

  public Task RunTask(Action callback, CancellationToken? cancellationToken = null) => RunTask((_) => callback(), cancellationToken);
  public Task RunTask(Action<CancellationToken> callback, CancellationToken? cancellationToken = null)
  {
    cancellationToken?.ThrowIfCancellationRequested();

    return RunTask((cancellationToken) =>
    {
      callback(cancellationToken);
      return Task.CompletedTask;
    }, cancellationToken);
  }

  public async Task Start(CancellationToken cancellationToken)
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
