using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Services;

using Logging;

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

    protected sealed override Task OnRun(object data, CancellationToken cancellationToken) =>
        OnRun(cancellationToken);

    protected sealed override Task<object> OnStart(CancellationToken cancellationToken) =>
        Task.FromResult(new object());

    protected sealed override Task OnStop(object data, Exception? exception) => OnStop(exception);

    public new async Task Start(CancellationToken cancellationToken = default) =>
        await base.Start(cancellationToken);

    public new async Task Stop() => await base.Stop();

    public new async Task Join(CancellationToken cancellationToken = default) =>
        await base.Join(cancellationToken);
}

public abstract class Service2<D> : IService2
    where D : class
{
    private static TaskFactory? taskFactory;

    private static TaskFactory GetTaskFactory() =>
        taskFactory ??= new TaskFactory(
            TaskCreationOptions.LongRunning,
            TaskContinuationOptions.LongRunning
        );

    private sealed record ServiceInstanceData(
        Task Task,
        CancellationTokenSource CancellationTokenSource,
        Func<Service2State> GetState
    )
    {
        public Service2State State => GetState();
    }

    public Service2(string name, IService2 downstream)
        : this(name, downstream.Logger) { }

    public Service2(string name, Logger? downstream = null)
    {
        Name = name;
        logger = new(name);

        if (downstream != null)
        {
            downstream.Subscribe(logger);
        }

        logger.Logged += (level, scope, message, timestamp) =>
            Logged?.Invoke(level, scope, message, timestamp);
    }

    private Exception? lastException;
    private ServiceInstanceData? serviceInstanceData;
    private readonly Logger logger;

    public readonly string Name;

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<Service2State>? StateChanged;
    public event LoggerHandler? Logged;

    public Service2State State => serviceInstanceData?.GetState() ?? Service2State.NotRunning;

    protected abstract Task<D> OnStart(CancellationToken cancellationToken);

    protected virtual Task OnRun(D data, CancellationToken cancellationToken) =>
        Task.Delay(-1, cancellationToken);

    protected virtual Task OnStop(D data, Exception? exception) => Task.CompletedTask;

    public async Task Join(CancellationToken cancellationToken = default)
    {
        if (lastException != null)
        {
            ExceptionDispatchInfo.Throw(lastException);
        }

        Task task;

        lock (this)
        {
            task = serviceInstanceData?.Task ?? Task.CompletedTask;
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
        }
    }

    public async Task Start(CancellationToken startupCancellationToken = default)
    {
        CancellationTokenSource cancellationTokenSource = new();
        Service2State currentState = Service2State.NotRunning;

        void setState(Service2State state)
        {
            Service2State oldState = currentState;
            lock (this)
            {
                currentState = state;
            }

            StateChanged?.Invoke(this, state);
            logger.Debug($"[State]: {oldState} -> {state}");
        }

        void setLastException(Exception? exception)
        {
            lock (this)
            {
                lastException = exception;
            }

            if (exception != null)
            {
                ExceptionThrown?.Invoke(this, exception);
            }
        }

        TaskCompletionSource startupTaskCompletionSource = new();

        async Task runStage1(CancellationToken cancellationToken)
        {
            D data;

            setState(Service2State.StartingUp);

            try
            {
                data = await runStage2Startup(cancellationToken, startupCancellationToken);
            }
            catch (Exception exception)
            {
                setState(Service2State.CrashingDown);
                setLastException(exception);
                setState(Service2State.Crashed);
                throw;
            }

            setState(Service2State.Running);
            startupTaskCompletionSource.SetResult();

            try
            {
                await runStage2Run(data, cancellationToken);
            }
            catch (Exception exception)
            {
                setState(Service2State.CrashingDown);

                try
                {
                    await runStage2Stop(data, exception);
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
                await runStage2Stop(data, null);
            }
            catch (Exception exception)
            {
                setState(Service2State.CrashingDown);

                setLastException(exception);
                throw;
            }

            setState(Service2State.NotRunning);
        }

        async Task<D> runStage2Startup(
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

        async Task runStage2Run(D data, CancellationToken cancellationToken)
        {
            try
            {
                await OnRun(data, cancellationToken);
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

        async Task runStage2Stop(D data, Exception? exception)
        {
            try
            {
                await OnStop(data, exception);
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
            setLastException(null);
            serviceInstanceData = new(
                Service2<D>
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
                                catch (AggregateException exception)
                                {
                                    ExceptionDispatchInfo.Throw(exception.GetBaseException());
                                }
                            }
                            finally
                            {
                                lock (this)
                                {
                                    serviceInstanceData = null;
                                }
                            }
                        },
                        CancellationToken.None
                    ),
                cancellationTokenSource,
                () => currentState
            );
        }

        await startupTaskCompletionSource.Task;
    }

    public async Task Stop()
    {
        ServiceInstanceData? serviceInstanceData;

        lock (this)
        {
            serviceInstanceData = this.serviceInstanceData;

            if (
                serviceInstanceData == null
                || !(
                    serviceInstanceData.State == Service2State.Running
                    || serviceInstanceData.State == Service2State.StartingUp
                )
            )
            {
                return;
            }

            try
            {
                serviceInstanceData.CancellationTokenSource.Cancel();
            }
            catch { }
        }

        if (serviceInstanceData == null)
        {
            return;
        }

        try
        {
            await serviceInstanceData.Task;
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
