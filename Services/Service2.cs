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
    Running = 0b00100,
    NotRunning = 0b01000,
    Pending = 0b00010,
    Errored = 0b00001,
    Cancelled = 0b10000,

    CancelledWhileRunning = Running | Cancelled,
    Starting = Running | Pending,
    Stopping = NotRunning | Pending,
    Crashing = NotRunning | Pending | Errored,
    Crashed = NotRunning | Errored
}

public abstract class Service2 : Service2<object>
{
    protected Service2(string name, IService2 downstream)
        : base(name, downstream) { }

    protected Service2(string name, Logger? downstream = null)
        : base(name, downstream) { }

    protected abstract Task OnRun(CancellationToken cancellationToken);
    protected abstract Task OnStop(Exception? exception);

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
            logger.Subscribe(logger);
        }
    }

    private Exception? lastException;
    private ServiceInstanceData? serviceInstanceData;
    private readonly Logger logger;

    public readonly string Name;

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<Service2State>? StateChanged;

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

        await task.WaitAsync(cancellationToken);
    }

    public async Task Start(CancellationToken startupCancellationToken = default)
    {
        CancellationTokenSource cancellationTokenSource = new();
        Service2State currentState = Service2State.Running | Service2State.Pending;

        void setState(Service2State state)
        {
            lock (this)
            {
                currentState = state;
            }

            StateChanged?.Invoke(this, state);
        }

        TaskCompletionSource startupTaskCompletionSource = new();

        async Task<D> runStage3Startup(
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
                D result = await OnStart(startupLinkedCancellationTokenSource.Token);
                startupTaskCompletionSource.SetResult();

                return result;
            }
            catch
            {
                throw;
            }
        }

        async Task runStage3Run(D data, CancellationToken cancellationToken)
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

        async Task runStage3Stop(D data, Exception? exception)
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

        async Task runStage2(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => setState(currentState | Service2State.Cancelled));

            setState(Service2State.Running | Service2State.Pending);
            D data;

            try
            {
                data = await runStage3Startup(cancellationToken, startupCancellationToken);
            }
            catch (Exception exception)
            {
                setState(Service2State.NotRunning | Service2State.Errored);

                lastException = exception;
                throw;
            }

            setState(Service2State.Running);

            try
            {
                try
                {
                    await runStage3Run(data, cancellationToken);
                }
                catch (Exception exception)
                {
                    setState(
                        Service2State.NotRunning | Service2State.Errored | Service2State.Pending
                    );

                    await runStage3Stop(data, exception);
                    throw;
                }
            }
            catch (Exception exception)
            {
                setState(Service2State.NotRunning | Service2State.Errored);

                lastException = exception;
                throw;
            }

            setState(Service2State.NotRunning | Service2State.Pending);

            try
            {
                await runStage3Stop(data, null);
            }
            catch (Exception exception)
            {
                setState(Service2State.NotRunning | Service2State.Errored | Service2State.Pending);

                lastException = exception;
                throw;
            }

            setState(Service2State.NotRunning);
        }

        async Task runStage1(CancellationToken cancellationToken)
        {
            try
            {
                await runStage2(cancellationToken);
            }
            finally
            {
                lock (this)
                {
                    serviceInstanceData = null;
                }
            }
        }

        lock (this)
        {
            lastException = null;
            serviceInstanceData = new(
                Service2<D>
                    .GetTaskFactory()
                    .StartNew(
                        () =>
                        {
                            try
                            {
                                runStage1(cancellationTokenSource.Token).Wait();
                            }
                            catch (AggregateException exception)
                            {
                                ExceptionDispatchInfo.Throw(exception.GetBaseException());
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

            if (serviceInstanceData == null || serviceInstanceData.State != Service2State.Running)
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
