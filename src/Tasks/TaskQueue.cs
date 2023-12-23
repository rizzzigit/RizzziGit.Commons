namespace RizzziGit.Framework.Tasks;

using Collections;

internal class TaskQueue : IDisposable
{
  private readonly WaitQueue<(Func<CancellationToken, Task> callback, CancellationToken cancellationToken, TaskCompletionSource taskCompletionSource)> WaitQueue = new();

  void IDisposable.Dispose() => WaitQueue.Dispose();
  public void Dispose(Exception? exception) => WaitQueue.Dispose(exception);

  public async Task RunTask(Func<CancellationToken, Task> callback, CancellationToken? cancellationToken = null)
  {
    TaskCompletionSource taskCompletionSource = new();
    await WaitQueue.Enqueue((callback, cancellationToken ?? CancellationToken.None, taskCompletionSource), cancellationToken ?? CancellationToken.None);
    await taskCompletionSource.Task;
  }

  public async Task<T> RunTask<T>(Func<CancellationToken, Task<T>> callback, CancellationToken? cancellationToken = null)
  {
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

  public Task<T> RunTask<T>(Func<T> callback) => RunTask((_) => Task.FromResult(callback()));
  public Task RunTask(Action callback) => RunTask((_) =>
  {
    callback();
    return Task.CompletedTask;
  });

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
