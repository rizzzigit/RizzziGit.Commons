namespace RizzziGit.Framework.Services;

using Logging;
using Tasks;

public enum ServiceState : byte
{
  Starting = 0b110,
  Running = 0b100,
  Stopping = 0b010,
  NotRunning = 0b000,
  Crashed = 0b001
}

public abstract class Service
{
  private class ServiceContext(Func<ServiceState> getState, Func<Task> getTask, Func<CancellationTokenSource> getCancellationTokenSource)
  {
    public ServiceState State => getState();
    public Task Task = getTask();
    public CancellationTokenSource CancellationTokenSource => getCancellationTokenSource();
  }

  private static TaskFactory? Factory = null;

  protected Service(string? name, Service downstreamLogger) : this(name, downstreamLogger.Logger) { }
  protected Service(string? name, Logger? downstreamLogger = null)
  {
    Factory ??= new(TaskCreationOptions.LongRunning, TaskContinuationOptions.LongRunning);
    Logger = new(Name = name ?? GetType().Name);

    downstreamLogger?.Subscribe(Logger);
  }

  public readonly Logger Logger;
  public readonly string Name;

  public ServiceState State => Context?.State ?? ServiceState.NotRunning;
  public event EventHandler<ServiceState>? StateChanged;

  private ServiceContext? Context;

  protected CancellationToken GetCancellationToken()
  {
    lock (this)
    {
      return Context?.CancellationTokenSource.Token ?? new(true);
    }
  }

  public Task Stop()
  {
    ServiceContext? context = Context;

    try { context?.CancellationTokenSource.Cancel(); } catch { }
    return (context?.Task ?? Task.CompletedTask).ContinueWith((_) => { });
  }

  public Task Join() => Context?.Task ?? Task.CompletedTask;

  private bool IsRunning = false;
  public async Task Start(CancellationToken startupCancellationToken = default)
  {
    lock (this)
    {
      if (IsRunning)
      {
        throw new InvalidOperationException("Service is running.");
      }

      IsRunning = true;
    }

    ServiceState state = ServiceState.NotRunning;
    TaskCompletionSource startupTaskCompletionSource = new();
    _ = Factory!.StartNew(async () =>
    {
      Logger.Log(LogLevel.Debug, $"Running on thread: \"{Thread.CurrentThread.Name}\" (#{Environment.CurrentManagedThreadId}).");
      TaskCompletionSource serviceCompletionSource = new();
      using CancellationTokenSource serviceCancellationTokenSource = new();

      ServiceContext context = Context = new(() => state, () => serviceCompletionSource.Task, () => serviceCancellationTokenSource);
      try
      {
        try
        {
          setState(ServiceState.Starting);
          await OnStart(startupCancellationToken);
          setState(ServiceState.Running);
        }
        catch (Exception onStartException)
        {
          logException(onStartException);
          setState(ServiceState.Stopping);

          try { await OnStop(onStartException); }
          catch (Exception onStopException)
          {
            logException(onStopException);
            Exception aggregateException = new AggregateException(onStartException, onStopException);

            setState(ServiceState.Crashed);
            serviceCompletionSource.SetException(aggregateException);
            startupTaskCompletionSource.SetException(aggregateException);
            return;
          }

          setState(ServiceState.Crashed);
          serviceCompletionSource.SetException(onStartException);
          startupTaskCompletionSource.SetException(onStartException);
          return;
        }

        startupTaskCompletionSource.TrySetResult();

        try
        {
          await OnRun(serviceCancellationTokenSource.Token);
        }
        catch (Exception onRunException)
        {
          if ((onRunException is not OperationCanceledException onStopCancelledException) || (serviceCancellationTokenSource.Token != onStopCancelledException.CancellationToken))
          {
            logException(onRunException);
            setState(ServiceState.Stopping);

            try { await OnStop(onRunException); }
            catch (Exception onStopException)
            {
              logException(onStopException);
              Exception aggregateException = new AggregateException(onRunException, onStopException);

              setState(ServiceState.Crashed);
              serviceCompletionSource.SetException(aggregateException);
              return;
            }

            setState(ServiceState.Crashed);
            serviceCompletionSource.SetException(onRunException);
            return;
          }
        }

        setState(ServiceState.Stopping);
        try
        {
          await OnStop();
        }
        catch (Exception onStopException)
        {
          if ((onStopException is not OperationCanceledException onStopCancelledException) || (serviceCancellationTokenSource.Token != onStopCancelledException.CancellationToken))
          {
            logException(onStopException);

            setState(ServiceState.Crashed);
            serviceCompletionSource.SetException(onStopException);
            return;
          }
        }

        setState(ServiceState.NotRunning);
        serviceCompletionSource.SetResult();
      }
      finally
      {
        IsRunning = false;
        serviceCancellationTokenSource.Dispose();
      }
    }, TaskCreationOptions.LongRunning);

    await startupTaskCompletionSource.Task;

    void logException(Exception exception) => Logger.Log(LogLevel.Fatal, $"Fatal error when {state}: [{exception.GetType().Name}] {exception.Message}{(exception.StackTrace != null ? $"\n{exception.StackTrace}" : "")}");
    void setState(ServiceState newState)
    {
      state = newState;
      {
        string? message = state switch
        {
          ServiceState.Starting => "Starting...",
          ServiceState.Running => "Is now running.",
          ServiceState.Stopping => "Stopping...",
          ServiceState.NotRunning => "Stopped.",
          ServiceState.Crashed => "Crashed.",

          _ => null
        };

        if (message != null)
        {
          Logger.Log(LogLevel.Debug, message);
        }
      }
      StateChanged?.Invoke(this, state);
    }
  }

  protected static async Task WatchDog(Service[] services, CancellationToken cancellationToken)
  {
    List<Task> tasks = [];
    foreach (Service service in services)
    {
      tasks.Add(service.Join());
    }

    Task task = await Task.WhenAny(tasks).WaitAsync(cancellationToken);

    try { await task; }
    catch (Exception exception)
    {
      if (exception is OperationCanceledException operationCanceledException && cancellationToken == operationCanceledException.CancellationToken)
      {
        return;
      }

      throw new WatchDogException(services[tasks.IndexOf(task)], exception);
    }
  }

  protected virtual Task OnStart(CancellationToken cancellationToken) => Task.CompletedTask;
  protected virtual Task OnRun(CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);
  protected virtual Task OnStop(Exception? exception = null) => Task.CompletedTask;
}
