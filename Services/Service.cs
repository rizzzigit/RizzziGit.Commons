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
    private ServiceInstance? instance;
    private readonly Logger logger;

    public readonly string Name;

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<ServiceState>? StateChanged;
    public ServiceState State => instance?.State ?? ServiceState.NotRunning;

    private ServiceInstance InternalContext =>
        instance ?? throw new InvalidOperationException("Service is not running.");

    protected CancellationToken GetServiceCancellationToken() =>
        InternalContext.CancellationTokenSource.Token;

    protected C GetContext() =>
        (
            InternalContext.Context
            ?? throw new InvalidOperationException("Context has not yet been initialized")
        ).Value!;

    protected abstract Task<C> OnStart(CancellationToken startupCancellationToken);

    protected virtual Task OnRun(C context, CancellationToken serviceCancellationToken) =>
        Task.Delay(-1, serviceCancellationToken);

    protected virtual Task OnStop(C context, ExceptionDispatchInfo? exception) =>
        Task.CompletedTask;

    private void SetState(ServiceInstance instance, ServiceState state)
    {
        Debug($"{instance.State} -> {instance.State = state}", "State");
        StateChanged?.Invoke(this, state);
    }

    public async Task Start(CancellationToken startupCancellationToken = default)
    {
        CancellationTokenSource serviceCancellationTokenSource = new();
        TaskCompletionSource startupTaskCompletionSource = new();

        lock (this)
        {
            if (instance != null)
            {
                throw new InvalidOperationException("Service is already running.");
            }

            lastException = null;
            TaskCompletionSource<ServiceInstance> instanceSource = new();

            async Task run()
            {
                await RunInternal(
                    await instanceSource.Task,
                    startupTaskCompletionSource,
                    serviceCancellationTokenSource,
                    startupCancellationToken
                );
            }

            instance = new()
            {
                Task = run(),

                CancellationTokenSource = serviceCancellationTokenSource,
                State = ServiceState.NotRunning,
                Context = null,

                PostRunWaitList = [],
                PostRunWaitListSemaphore = new(1),

                ChildSeviceList = [],
                ChildSeviceListSemaphore = new(1),
            };

            instanceSource.SetResult(instance);
        }

        await startupTaskCompletionSource.Task;
    }

    public async Task Stop()
    {
        ServiceInstance? serviceInstanceContext;

        lock (this)
        {
            serviceInstanceContext = instance;

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
