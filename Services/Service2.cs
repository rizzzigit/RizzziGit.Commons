using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Services;

using Logging;
using Utilities;

public class Service2Exception<D>(Service2<D> service) : Exception
    where D : class
{
    public readonly Service2<D> Service = service;
}

public enum Service2State : byte
{
    NotRunning,
    StartingUp,
    Running,
    ShuttingDown,
    CrashingDown,
    Crashed,
}

public abstract class Service2 : Service2<object>
{
    protected Service2(string name, IService2 downstream)
        : base(name, downstream) { }

    protected Service2(string name, Logger? downstream = null)
        : base(name, downstream) { }

    protected virtual Task OnRun(CancellationToken cancellationToken) =>
        Task.Delay(-1, cancellationToken);

    protected virtual Task OnStop(Exception? exception) => Task.CompletedTask;

    protected sealed override Task OnRun(object context, CancellationToken cancellationToken) =>
        OnRun(cancellationToken);

    protected sealed override Task<object> OnStart(CancellationToken cancellationToken) =>
        Task.FromResult(new object());

    protected sealed override Task OnStop(object context, Exception? exception) =>
        OnStop(exception);

    public new async Task Start(CancellationToken cancellationToken = default) =>
        await base.Start(cancellationToken);

    public new async Task Stop() => await base.Stop();

    public new async Task Join(CancellationToken cancellationToken = default) =>
        await base.Join(cancellationToken);
}

public abstract class Service2<C> : IService2
    where C : notnull
{
    public static async Task StartServices(
        IService2[] services,
        CancellationToken cancellationToken = default
    )
    {
        List<IService2> startedServices = [];
        try
        {
            foreach (IService2 service in services)
            {
                await service.Start(cancellationToken);

                startedServices.Add(service);
            }
        }
        catch (Exception exception)
        {
            List<ExceptionDispatchInfo> stopExceptions = [];

            foreach (IService2 service in services)
            {
                try
                {
                    await service.Stop();
                }
                catch (Exception stopException)
                {
                    stopExceptions.Add(ExceptionDispatchInfo.Capture(stopException));
                }
            }

            if (stopExceptions.Count == 0)
            {
                throw;
            }

            throw new AggregateException(
                [exception, .. stopExceptions.Select((exception) => exception.SourceException)]
            );
        }
    }

    public static async Task StopServices(params IService2[] services)
    {
        List<ExceptionDispatchInfo> stopExceptions = [];

        foreach (IService2 service in services)
        {
            try
            {
                await service.Stop();
            }
            catch (Exception exception)
            {
                stopExceptions.Add(ExceptionDispatchInfo.Capture(exception));
            }
        }

        if (stopExceptions.Count == 0)
        {
            return;
        }

        throw new AggregateException(
            [.. stopExceptions.Select((exception) => exception.SourceException)]
        );
    }

    private static TaskFactory? taskFactory;

    private static TaskFactory GetTaskFactory() =>
        taskFactory ??= new TaskFactory(
            TaskCreationOptions.LongRunning,
            TaskContinuationOptions.LongRunning
        );

    private sealed record ServiceInstanceContext(
        Task Task,
        CancellationTokenSource CancellationTokenSource,
        Func<Service2State> GetState,
        Func<StrongBox<C>?> GetContext
    ) { }

    public Service2(string name, IService2 downstream)
        : this(name, downstream.Logger) { }

    public Service2(string name, Logger? downstream = null)
    {
        Name = name;
        logger = new(name);

        downstream?.Subscribe(logger);

        logger.Logged += (level, scope, message, timestamp) =>
            Logged?.Invoke(level, scope, message, timestamp);
    }

    private ExceptionDispatchInfo? lastException;
    private ServiceInstanceContext? serviceContext;
    private readonly Logger logger;

    public readonly string Name;

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<Service2State>? StateChanged;
    public event LoggerHandler? Logged;

    protected void Log(LogLevel level, string message, string? scope = null) =>
        logger.Log(level, $"{(scope != null ? $"[{scope}] " : "")}{message}");

    protected void Debug(string message, string? scope = null) =>
        Log(LogLevel.Debug, message, scope);

    protected void Info(string message, string? scope = null) => Log(LogLevel.Info, message, scope);

    protected void Warn(string message, string? scope = null) => Log(LogLevel.Warn, message, scope);

    protected void Error(string message, string? scope = null) =>
        Log(LogLevel.Error, message, scope);

    protected void Fatal(string message, string? scope = null) =>
        Log(LogLevel.Fatal, message, scope);

    protected void Warn(Exception exception, string? scope = null) =>
        Warn(exception.ToPrintable(), scope);

    protected void Error(Exception exception, string? scope = null) =>
        Error(exception.ToPrintable(), scope);

    protected void Fatal(Exception exception, string? scope = null) =>
        Fatal(exception.ToPrintable(), scope);

    public Service2State State => serviceContext?.GetState() ?? Service2State.NotRunning;
    protected C Context =>
        (
            serviceContext?.GetContext()
            ?? throw new InvalidOperationException("Context has has not yet been initialized")
        ).Value!;

    protected abstract Task<C> OnStart(CancellationToken cancellationToken);

    protected virtual Task OnRun(C context, CancellationToken cancellationToken) =>
        Task.Delay(-1, cancellationToken);

    protected virtual Task OnStop(C context, Exception? exception) => Task.CompletedTask;

    public async Task Join(CancellationToken cancellationToken = default)
    {
        lastException?.Throw();

        Task task;

        lock (this)
        {
            task = serviceContext?.Task ?? Task.CompletedTask;
        }

        try
        {
            await task.WaitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            if (
                exception is OperationCanceledException operationCanceledException
                && operationCanceledException.CancellationToken == cancellationToken
            )
            {
                return;
            }

            throw;
        }
    }

    protected async Task Start(CancellationToken startupCancellationToken = default)
    {
        CancellationTokenSource cancellationTokenSource = new();
        Service2State currentState = Service2State.NotRunning;
        StrongBox<C>? currentContext = null;

        void setState(Service2State state)
        {
            Debug($"{currentState} -> {currentState = state}", "State");
            StateChanged?.Invoke(this, state);
        }

        TaskCompletionSource startupTaskCompletionSource = new();

        async Task runStage1(CancellationToken cancellationToken)
        {
            setState(Service2State.StartingUp);

            try
            {
                currentContext = new(
                    await runStage2Startup(cancellationToken, startupCancellationToken)
                );
            }
            catch
            {
                setState(Service2State.CrashingDown);
                setState(Service2State.Crashed);
                throw;
            }

            setState(Service2State.Running);
            startupTaskCompletionSource.SetResult();

            try
            {
                await runStage2Run(currentContext!.Value!, cancellationToken);
            }
            catch (Exception exception)
            {
                setState(Service2State.CrashingDown);

                try
                {
                    await runStage2Stop(currentContext!.Value!, exception);
                }
                catch (Exception stopException)
                {
                    setState(Service2State.Crashed);
                    throw new AggregateException(exception, stopException);
                }

                setState(Service2State.Crashed);
                throw;
            }

            setState(Service2State.ShuttingDown);

            try
            {
                await runStage2Stop(currentContext!.Value!, null);
            }
            catch
            {
                setState(Service2State.CrashingDown);
                setState(Service2State.Crashed);
                throw;
            }

            setState(Service2State.NotRunning);
        }

        async Task<C> runStage2Startup(
            CancellationToken cancellationToken,
            CancellationToken startupCancellationToken
        )
        {
            using CancellationTokenSource startupLinkedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    startupCancellationToken,
                    cancellationToken
                );
            try
            {
                return await OnStart(startupLinkedCancellationTokenSource.Token);
            }
            catch (Exception exception)
            {
                startupTaskCompletionSource.SetException(exception);
                throw;
            }
        }

        async Task runStage2Run(C context, CancellationToken cancellationToken)
        {
            try
            {
                await OnRun(context, cancellationToken);
            }
            catch (Exception exception)
            {
                if (
                    exception is OperationCanceledException operationCanceledException
                    && (operationCanceledException.CancellationToken == cancellationToken)
                )
                {
                    return;
                }

                throw;
            }
        }

        async Task runStage2Stop(C context, Exception? exception)
        {
            try
            {
                await OnStop(context, exception);
            }
            catch (Exception e)
            {
                if (exception != null)
                {
                    throw new AggregateException(exception, e);
                }

                throw;
            }
        }

        lock (this)
        {
            lastException = null;
            serviceContext = new(
                Service2<C>
                    .GetTaskFactory()
                    .StartNew(
                        () =>
                        {
                            try
                            {
                                try
                                {
                                    runStage1(cancellationTokenSource.Token).Wait();
                                }
                                catch (AggregateException aggregateException)
                                {
                                    ExceptionDispatchInfo exception = ExceptionDispatchInfo.Capture(
                                        aggregateException.GetBaseException()
                                    );

                                    lock (this)
                                    {
                                        lastException = exception;
                                        Fatal(
                                            "Fatal Error",
                                            $"{exception.SourceException.Message}{(exception.SourceException.StackTrace != null ? $" {exception.SourceException.StackTrace}" : "")}"
                                        );
                                        ExceptionThrown?.Invoke(this, exception.SourceException);
                                    }

                                    exception.Throw();
                                }
                            }
                            finally
                            {
                                lock (this)
                                {
                                    serviceContext = null;
                                }
                            }
                        },
                        CancellationToken.None
                    ),
                cancellationTokenSource,
                () => currentState,
                () => currentContext
            );
        }

        await startupTaskCompletionSource.Task;
    }

    public async Task Stop()
    {
        ServiceInstanceContext? serviceInstanceContext;

        lock (this)
        {
            serviceInstanceContext = this.serviceContext;

            if (serviceInstanceContext == null)
            {
                return;
            }
            else
            {
                Service2State state = serviceInstanceContext.GetState();

                if (!(state == Service2State.Running || state == Service2State.StartingUp))
                {
                    return;
                }
            }

            try
            {
                serviceInstanceContext.CancellationTokenSource.Cancel();
            }
            catch { }
        }

        if (serviceInstanceContext == null)
        {
            return;
        }

        try
        {
            await serviceInstanceContext.Task;
        }
        catch { }
    }

    string IService2.Name => Name;
    Logger IService2.Logger => logger;

    Task IService2.Join(CancellationToken cancellationToken) => Join(cancellationToken);

    Task IService2.Start(CancellationToken cancellationToken) => Start(cancellationToken);

    Task IService2.Stop() => Stop();
}

public interface IService2
{
    public string Name { get; }
    public Service2State State { get; }
    public Logger Logger { get; }

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<Service2State>? StateChanged;

    public Task Join(CancellationToken cancellationToken = default);
    public Task Start(CancellationToken cancellationToken = default);
    public Task Stop();
}
