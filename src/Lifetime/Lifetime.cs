namespace RizzziGit.Framework.Lifetime;

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

  public CancellationToken GetCancellationToken() => Source?.Token ?? new(true);
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
        Logger.Log(LogLevel.Verbose, "Lifetime started.");
        try
        {
          Started?.Invoke(this, new());
          await OnRun(linkedCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Exception = exception; }
        finally
        {
          lock (this)
          {
            Source = null;
          }

          Logger.Log(LogLevel.Verbose, "Lifetime stopped.");
          Stopped?.Invoke(this, new());
        }
      }
    }
  }

  public virtual void Start(CancellationToken cancellationToken = default)
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

  protected void Run(Action action, CancellationToken cancellationToken = default) => Run((_) => action(), cancellationToken);
  protected void Run(Action<CancellationToken> action, CancellationToken cancellationToken = default)
  {
    CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetCancellationToken());

    linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
    lock (this)
    {
      linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
      action(linkedCancellationTokenSource.Token);
    }
  }

  protected T Run<T>(Func<T> function, CancellationToken cancellationToken = default) => Run((_) => function(), cancellationToken);
  protected T Run<T>(Func<CancellationToken, T> function, CancellationToken cancellationToken = default)
  {
    CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetCancellationToken());

    linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
    lock (this)
    {
      linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
      return function(linkedCancellationTokenSource.Token);
    }
  }
}
