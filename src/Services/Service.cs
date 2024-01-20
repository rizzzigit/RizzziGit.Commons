namespace RizzziGit.Framework.Services;

using Logging;
using Tasks;

public enum ServiceState : byte
{
  Starting = 0b110,
  Started = 0b100,
  Stopping = 0b010,
  Stopped = 0b000,
  Crashed = 0b001
}

public abstract class Service
{
  private class ServiceContext(Task task, CancellationTokenSource stopToken)
  {
    public readonly Task Task = task;
    public readonly CancellationTokenSource CancellationTokenSource = stopToken;
  }

  private static TaskFactory? Factory = null;

  public Service() : this(null) { }
  public Service(string? name) : this(name, (Logger?)null) { }

  public Service(string? name, Service? downstreamLogger) : this(name, downstreamLogger?.Logger) { }
  public Service(string? name, Logger? downstreamLogger)
  {
    Factory ??= new(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

    Name = name ?? GetType().Name;
    State = ServiceState.Stopped;
    Logger = new(Name);

    downstreamLogger?.Subscribe(Logger);
  }

  public readonly Logger Logger;
  public readonly string Name;
  public ServiceState State;

  public event EventHandler<ServiceState>? StateChanged;

  private ServiceContext? Context;

  public CancellationToken GetCancellationToken() => Context?.CancellationTokenSource.Token ?? new(true);

  private void SetState(ServiceState state)
  {
    if (State == state)
    {
      return;
    }

    Logger.Log(LogLevel.Verbose, state switch
    {
      ServiceState.Starting => "Starting...",
      ServiceState.Started => "Started",
      ServiceState.Stopping => "Stopping...",
      ServiceState.Stopped => "Stopped.",
      ServiceState.Crashed => "Crashed.",

      _ => throw new NotImplementedException()
    });
    State = state;
    StateChanged?.Invoke(this, state);
  }

  public async Task Start()
  {
    if (Context?.Task.IsCompleted == false)
    {
      return;
    }

    Context = null;

    TaskCompletionSource serviceStartupSource = new();
    TaskCompletionSource<Task> serviceRunnerSource = new();
    TaskCompletionSource serviceRunnerStartTriggerSource = new();

    _ = Factory!.StartNew(async () =>
    {
      Thread.CurrentThread.Name = Name;
      Task task = RunThread(serviceStartupSource, serviceRunnerStartTriggerSource.Task);
      serviceRunnerSource.SetResult(task);
      serviceRunnerStartTriggerSource.SetResult();

      await task;
    });

    Context = new(await serviceRunnerSource.Task, new());
    await serviceStartupSource.Task;
  }

  private async Task RunThread(TaskCompletionSource startSource, Task onStart)
  {
    await onStart;

    try
    {
      await Run(Context!, startSource);
    }
    finally
    {
      Context!.CancellationTokenSource.Dispose();
    }
  }

  private async Task Run(ServiceContext context, TaskCompletionSource? startSource)
  {
    context.CancellationTokenSource.Token.ThrowIfCancellationRequested();
    try
    {
      SetState(ServiceState.Starting);
      await OnStart(context.CancellationTokenSource.Token);

      SetState(ServiceState.Started);
      startSource?.SetResult();
    }
    catch (OperationCanceledException)
    {
      Logger.Log(LogLevel.Info, $"Startup was cancelled on {Name}.");
    }
    catch (Exception exception)
    {
      SetState(ServiceState.Stopping);

      try
      {
        await OnStop(exception);
      }
      catch (Exception stopException)
      {
        Logger.Log(LogLevel.Fatal, $"Fatal Startup Exception on {Name}: {exception.GetType().FullName}: {stopException.Message}\n{stopException.StackTrace}");
        SetState(ServiceState.Crashed);
        startSource?.SetException(exception);
        throw new AggregateException(exception, stopException);
      }

      Logger.Log(LogLevel.Fatal, $"Fatal Startup Exception on {Name}: {exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}");
      SetState(ServiceState.Crashed);
      startSource?.SetException(exception);
      throw;
    }

    context.CancellationTokenSource.Token.ThrowIfCancellationRequested();
    try
    {
      using (context.CancellationTokenSource)
      {
        await OnRun(context.CancellationTokenSource.Token);
      }
    }
    catch (OperationCanceledException)
    {
      Logger.Log(LogLevel.Info, $"Operation was cancelled on {Name}.");
    }
    catch (Exception exception)
    {
      SetState(ServiceState.Stopping);

      try
      {
        await OnStop(exception);
      }
      catch (Exception stopException)
      {
        Logger.Log(LogLevel.Fatal, $"Fatal Exception on {Name}: {exception.GetType().FullName}: {stopException.Message}\n{stopException.StackTrace}");
        SetState(ServiceState.Crashed);
        throw new AggregateException(exception, stopException);
      }

      Logger.Log(LogLevel.Fatal, $"Fatal Exception on {Name}: {exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}");
      SetState(ServiceState.Crashed);
      throw;
    }

    SetState(ServiceState.Stopping);
    try
    {
      await OnStop(null);
    }
    catch
    {
      SetState(ServiceState.Crashed);
      throw;
    }
    SetState(ServiceState.Stopped);
  }

  public async Task Join()
  {
    if (Context != null) { await Context.Task; }
  }

  public async Task Stop()
  {
    ServiceContext? context = Context;
    if ((context == null) || context.CancellationTokenSource.IsCancellationRequested || context.Task.IsCompleted)
    {
      return;
    }

    try { context.CancellationTokenSource.Cancel(); } catch { }
    try { await context.Task; } catch { }
  }

  protected virtual Task OnStart(CancellationToken cancellationToken) => Task.CompletedTask;
  protected virtual Task OnRun(CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);
  protected virtual Task OnStop(Exception? exception) => Task.CompletedTask;

  protected static async Task<(Service service, Task task)> WatchDog(Service[] services, CancellationToken cancellationToken)
  {
    List<Task> tasks = [];
    foreach (Service service in services)
    {
      tasks.Add(service.Join().WaitAsync(cancellationToken));
    }

    Task task = await Task.WhenAny(tasks);
    return (services[tasks.IndexOf(task)], task);
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
