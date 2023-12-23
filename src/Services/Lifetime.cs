namespace RizzziGit.Framework.Services;

using Logging;
using Tasks;

public interface ILifetime
{
  public void Start(CancellationToken cancellationToken);
  public void Stop();
}

public abstract class Lifetime : ILifetime
{
  protected Lifetime(string name, Lifetime lifetime) : this(name, lifetime.Logger) { }
  protected Lifetime(string name, Logger? logger = null)
  {
    Name = name;
    Logger = new(name);

    logger?.Subscribe(Logger);
  }

  ~Lifetime() => Stop();

  public event EventHandler? Started;
  public event EventHandler? Stopped;

  public readonly string Name;
  public readonly Logger Logger;

  private CancellationTokenSource? Source;
  private readonly TaskQueue TaskQueue = new();

  public bool IsRunning
  {
    get
    {
      lock (this)
      {
        return Source != null;
      }
    }
  }

  public Exception? Exception { get; private set; } = null;
  private bool IsStarted;

  public Task RunTask(Func<CancellationToken, Task> callback, CancellationToken? cancellationToken = null) => TaskQueue!.RunTask(callback, cancellationToken);
  public Task<T> RunTask<T>(Func<CancellationToken, Task<T>> callback, CancellationToken? cancellationToken = null) => TaskQueue!.RunTask(callback, cancellationToken);
  public Task RunTask(Action callback) => TaskQueue!.RunTask(callback);
  public Task<T> RunTask<T>(Func<T> callback) => TaskQueue!.RunTask(callback);

  public CancellationToken GetCancellationToken() => Source!.Token;
  public void Reset()
  {
    lock (this)
    {
      if (!IsStarted)
      {
        return;
      }
      else if (IsRunning)
      {
        throw new InvalidOperationException("Cannot reset while running.");
      }

      IsStarted = false;
      Exception = null;
    }
  }

  public virtual void Stop() => Source?.Cancel();

  protected virtual Task OnRun(CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);

  private async void Run(CancellationTokenSource cancellationTokenSource, CancellationTokenSource linkedCancellationTokenSource)
  {
    using (linkedCancellationTokenSource)
    {
      linkedCancellationTokenSource.Token.Register(() =>
      {
        lock (this)
        {
          Source = null;
        }
      });

      using (cancellationTokenSource)
      {
        CancellationTokenSource taskQueueCancellationTokenSource = new();

        using (taskQueueCancellationTokenSource)
        {
          Task taskQueueTask = TaskQueue.Start(taskQueueCancellationTokenSource.Token);

          Logger.Log(LogLevel.Verbose, "Lifetime started.");
          try
          {
            Started?.Invoke(this, new());
            await OnRun(linkedCancellationTokenSource.Token);

            lock (this)
            {
              Source = null;
            }
          }
          catch (OperationCanceledException) { }
          catch (Exception exception) { Exception = exception; }
          finally
          {
            try
            {
              await taskQueueTask;
            }
            finally
            {
              Logger.Log(LogLevel.Verbose, "Lifetime stopped.");
              Stopped?.Invoke(this, new());
            }
          }
        }
      }
    }
  }

  public void Start(CancellationToken cancellationToken)
  {
    lock (this)
    {
      if (IsRunning)
      {
        throw new InvalidOperationException("Already running.");
      }
      else if (IsStarted)
      {
        throw new InvalidOperationException("Must be reset.");
      }

      Run(Source = new(), CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken, Source.Token
      ));
    }
  }
}
