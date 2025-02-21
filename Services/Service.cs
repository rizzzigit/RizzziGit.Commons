using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Services;

using Logging;
using Utilities;

public enum ServiceState : byte
{
    NotRunning,
    StartingUp,
    Running,
    ShuttingDown,
    CrashingDown,
    Crashed,
}

interface IServiceInternal : IService
{
    public Logger Logger { get; }
}

public abstract partial class Service<C> : IServiceInternal
    where C : notnull
{
    private static TaskFactory? taskFactory;

    private static TaskFactory GetTaskFactory() =>
        taskFactory ??= new TaskFactory(
            TaskCreationOptions.LongRunning,
            TaskContinuationOptions.LongRunning
        );

    public Service(string name, IService? downstream)
        : this(name, downstream is IServiceInternal internalService ? internalService.Logger : null)
    { }

    public Service(string name, Logger? downstream = null)
    {
        Name = name;
        logger = new(name);

        downstream?.Subscribe(logger);

        logger.Logged += (log) => Logged?.Invoke(log);
    }

    private ExceptionDispatchInfo? lastException;
    private ServiceInstance? internalContext;
    private readonly Logger logger;

    public readonly string Name;

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<ServiceState>? StateChanged;
    public ServiceState State => internalContext?.State ?? ServiceState.NotRunning;

    private ServiceInstance InternalContext =>
        internalContext ?? throw new InvalidOperationException("Service is not running.");

    protected CancellationToken GetCancellationToken() =>
        InternalContext.CancellationTokenSource.Token;

    protected C GetContext() =>
        (
            InternalContext.Context
            ?? throw new InvalidOperationException("Context has not yet been initialized")
        ).Value!;

    protected List<Task> WaitTasksBeforeStopping => InternalContext.WaitTasksBeforeStopping;

    protected abstract Task<C> OnStart(
        CancellationToken startupCancellationToken,
        CancellationToken serviceCancellationToken
    );

    protected virtual Task OnRun(C context, CancellationToken serviceCancellationToken) =>
        Task.Delay(-1, serviceCancellationToken);

    protected virtual Task OnStop(C context, ExceptionDispatchInfo? exception) =>
        Task.CompletedTask;

    private void SetState(ServiceInstance instance, ServiceState state)
    {
        Info($"{instance.State} -> {instance.State = state}", "State");
        StateChanged?.Invoke(this, state);
    }

    public async Task Start(CancellationToken startupCancellationToken = default)
    {
        CancellationTokenSource serviceCancellationTokenSource = new();
        TaskCompletionSource startupTaskCompletionSource = new();

        lock (this)
        {
            if (internalContext != null)
            {
                throw new InvalidOperationException("Service is already running.");
            }

            lastException = null;
            TaskCompletionSource<ServiceInstance> initiation = new();

            internalContext = new()
            {
                Task = Service<C>
                    .GetTaskFactory()
                    .StartNew(
                        () =>
                        {
                            try
                            {
                                RunInternal(
                                        initiation.Task.GetAwaiter().GetResult(),
                                        startupCancellationToken,
                                        startupTaskCompletionSource,
                                        serviceCancellationTokenSource
                                    )
                                    .GetAwaiter()
                                    .GetResult();
                            }
                            finally
                            {
                                lock (this)
                                {
                                    internalContext = null;
                                }
                            }
                        },
                        CancellationToken.None
                    ),
                CancellationTokenSource = serviceCancellationTokenSource,
                State = ServiceState.NotRunning,
                Context = null,
                WaitTasksBeforeStopping = [],
            };

            initiation.SetResult(internalContext);
        }

        await startupTaskCompletionSource.Task;
    }

    public async Task Stop()
    {
        ServiceInstance? serviceInstanceContext;

        lock (this)
        {
            serviceInstanceContext = internalContext;

            if (serviceInstanceContext == null)
            {
                return;
            }
            else
            {
                ServiceState state = serviceInstanceContext.State;

                if (!(state == ServiceState.Running || state == ServiceState.StartingUp))
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

    string IService.Name => Name;

    Logger IServiceInternal.Logger => logger;

    Task IService.Watch(CancellationToken cancellationToken) => Watch(cancellationToken);

    Task IService.Start(CancellationToken cancellationToken) => Start(cancellationToken);

    Task IService.Stop() => Stop();
}

public interface IService
{
    public string Name { get; }
    public ServiceState State { get; }

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<ServiceState>? StateChanged;

    public Task Watch(CancellationToken cancellationToken = default);
    public Task Start(CancellationToken cancellationToken = default);
    public Task Stop();
}
