namespace RizzziGit.Framework.Tasks;

using Collections;

public class TaskQueueEntryDefinition(Func<CancellationToken, Task> callback)
{
  public static implicit operator TaskQueueEntryDefinition(Func<CancellationToken, Task> callback) => new(callback);
  public static implicit operator Func<CancellationToken, Task>(TaskQueueEntryDefinition callback) => callback.Callback;
  public static implicit operator TaskQueueEntryDefinition(Func<Task> callback) => new((_) => callback());
  public static implicit operator TaskQueueEntryDefinition(Action callback) => new((_) =>
  {
    callback();
    return Task.CompletedTask;
  });
  public static implicit operator TaskQueueEntryDefinition(Action<CancellationToken> callback) => new((cancellationToken) =>
  {
    callback(cancellationToken);
    return Task.CompletedTask;
  });

  public readonly Func<CancellationToken, Task> Callback = callback;
}

public class TaskQueueEntryDefinition<T>(Func<CancellationToken, Task<T>> callback) : TaskQueueEntryDefinition((_) => Task.CompletedTask)
{
  public static implicit operator TaskQueueEntryDefinition<T>(Func<CancellationToken, Task<T>> callback) => new(callback);
  public static implicit operator TaskQueueEntryDefinition<T>(Func<Task<T>> callback) => new((_) => callback());
  public static implicit operator TaskQueueEntryDefinition<T>(Func<T> callback) => new((_) => Task.FromResult(callback()));
  public static implicit operator TaskQueueEntryDefinition<T>(Func<CancellationToken, T> callback) => new((cancellationToken) => Task.FromResult(callback(cancellationToken)));
  public static implicit operator TaskQueueEntryDefinition<T>(T obj) => new((_) => Task.FromResult(obj));

  public new readonly Func<CancellationToken, Task<T>> Callback = callback;
}

public sealed class TaskQueue : IDisposable
{
  private readonly WaitQueue<(Func<CancellationToken, Task> callback, CancellationToken cancellationToken, TaskCompletionSource taskCompletionSource)> WaitQueue = new();

  void IDisposable.Dispose() => WaitQueue.Dispose();
  public void Dispose(Exception? exception) => WaitQueue.Dispose(exception);

  private SynchronizationContext? SynchronizationContext;

  public Task RunTask(Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition)callback, cancellationToken);
  public Task RunTask(Action<CancellationToken> callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition)callback, cancellationToken);
  public Task RunTask(Func<Task> callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition)callback, cancellationToken);
  public Task RunTask(Action callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition)callback, cancellationToken);
  public Task RunTask(TaskQueueEntryDefinition definition, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (this)
    {
      return SynchronizationContext == SynchronizationContext.Current
        ? definition.Callback(cancellationToken)
        : run();
    }

    async Task run()
    {
      TaskCompletionSource taskCompletionSource = new();
      await WaitQueue.Enqueue((definition.Callback, cancellationToken, taskCompletionSource), cancellationToken);
      await taskCompletionSource.Task;
    }
  }

  public Task<T> RunTask<T>(Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition<T>)callback, cancellationToken);
  public Task<T> RunTask<T>(Func<CancellationToken, T> callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition<T>)callback, cancellationToken);
  public Task<T> RunTask<T>(Func<Task<T>> callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition<T>)callback, cancellationToken);
  public Task<T> RunTask<T>(Func<T> callback, CancellationToken cancellationToken = default) => RunTask((TaskQueueEntryDefinition<T>)callback, cancellationToken);
  public Task<T> RunTask<T>(TaskQueueEntryDefinition<T> definition, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (this)
    {
      return SynchronizationContext == SynchronizationContext.Current
        ? definition.Callback(cancellationToken)
        : run();
    }

    async Task<T> run()
    {
      TaskCompletionSource<T> source = new();

      await RunTask(async (cancellationToken) => source.SetResult(await definition.Callback(cancellationToken)), cancellationToken);
      return await source.Task;
    }
  }

  public Task Start(CancellationToken cancellationToken = default) => Task.Run(async () =>
  {
    lock (this)
    {
      if (SynchronizationContext != null)
      {
        throw new InvalidOperationException("Task queue has already started.");
      }

      SynchronizationContext = SynchronizationContext.Current;
    }

    try
    {
      await foreach (var (callback, remoteCancellationToken, taskCompletionSource) in WaitQueue.WithCancellation(cancellationToken))
      {
        try
        {
          using CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            remoteCancellationToken,
            cancellationToken
          );

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
      lock (this)
      {
        SynchronizationContext = null;
      }

      while (WaitQueue.Count != 0)
      {
        var (_, _, source) = await WaitQueue.Dequeue(cancellationToken);

        source.SetException(exception);
        throw;
      }
    }
  }, CancellationToken.None);
}
