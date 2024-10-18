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

public abstract partial class Service2<C> : IService2
    where C : notnull
{
    private static TaskFactory? taskFactory;

    private static TaskFactory GetTaskFactory() =>
        taskFactory ??= new TaskFactory(
            TaskCreationOptions.LongRunning,
            TaskContinuationOptions.LongRunning
        );

    public Service2(string name, IService2 downstream)
        : this(name, downstream?.Logger) { }

    public Service2(string name, Logger? downstream = null)
    {
        Name = name;
        logger = new(name);

        downstream?.Subscribe(logger);

        logger.Logged += (level, scope, message, timestamp) =>
            Logged?.Invoke(level, scope, message, timestamp);
    }

    private ExceptionDispatchInfo? lastException;
    private ServiceInstance? serviceContext;
    private readonly Logger logger;

    public readonly string Name;

    public event EventHandler<Exception>? ExceptionThrown;
    public event EventHandler<Service2State>? StateChanged;
    public Service2State State => serviceContext?.State ?? Service2State.NotRunning;

    protected CancellationToken CancellationToken =>
        serviceContext?.CancellationTokenSource.Token
        ?? throw new InvalidOperationException("Service is not running.");

    protected C Context =>
        (
            serviceContext?.Context
            ?? throw new InvalidOperationException("Context has has not yet been initialized")
        ).Value!;

    protected abstract Task<C> OnStart(CancellationToken cancellationToken);

    protected virtual Task OnRun(C context, CancellationToken cancellationToken) =>
        Task.Delay(-1, cancellationToken);

    protected virtual Task OnStop(C context, Exception? exception) => Task.CompletedTask;

    private void SetState(ServiceInstance instance, Service2State state)
    {
        Info($"{instance.State} -> {instance.State = state}", "State");
        StateChanged?.Invoke(this, state);
    }

    public async Task Start(CancellationToken startupCancellationToken = default)
    {
        CancellationTokenSource cancellationTokenSource = new();
        TaskCompletionSource startupTaskCompletionSource = new();

        lock (this)
        {
            lastException = null;
            TaskCompletionSource<ServiceInstance> initiation = new();

            serviceContext = new()
            {
                Task = Service2<C>
                    .GetTaskFactory()
                    .StartNew(
                        () =>
                        {
                            try
                            {
                                try
                                {
                                    RunInternal(
                                            initiation.Task.GetAwaiter().GetResult(),
                                            startupCancellationToken,
                                            startupTaskCompletionSource,
                                            cancellationTokenSource.Token
                                        )
                                        .GetAwaiter()
                                        .GetResult();
                                }
                                catch (Exception exception)
                                {
                                    lock (this)
                                    {
                                        lastException = ExceptionDispatchInfo.Capture(exception);
                                        Fatal(exception.ToPrintable(), "Fatal Error");
                                        ExceptionThrown?.Invoke(this, exception);
                                    }

                                    throw;
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
                CancellationTokenSource = cancellationTokenSource,
                State = Service2State.NotRunning,
                Context = null
            };

            initiation.SetResult(serviceContext);
        }

        await startupTaskCompletionSource.Task;
    }

    public async Task Stop()
    {
        ServiceInstance? serviceInstanceContext;

        lock (this)
        {
            serviceInstanceContext = this.serviceContext;

            if (serviceInstanceContext == null)
            {
                return;
            }
            else
            {
                Service2State state = serviceInstanceContext.State;

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

    Task IService2.Watch(CancellationToken cancellationToken) => Watch(cancellationToken);

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

    public Task Watch(CancellationToken cancellationToken = default);
    public Task Start(CancellationToken cancellationToken = default);
    public Task Stop();
}
