namespace RizzziGit.Commons.Services;

using System.Runtime.ExceptionServices;
using Logging;

public abstract class Service : Service<object>
{
    protected Service(string name, IService downstream)
        : base(name, downstream) { }

    protected Service(string name, Logger? downstream = null)
        : base(name, downstream) { }

    protected virtual Task OnRun(CancellationToken cancellationToken) =>
        Task.Delay(-1, cancellationToken);

    protected virtual Task OnStop(ExceptionDispatchInfo? exception) => Task.CompletedTask;

    protected sealed override Task OnRun(object context, CancellationToken cancellationToken) =>
        OnRun(cancellationToken);

    protected sealed override Task<object> OnStart(
        CancellationToken startupCancellationToken,
        CancellationToken serviceCancellationToken
    ) => Task.FromResult(new object());

    protected sealed override Task OnStop(object context, ExceptionDispatchInfo? exception) =>
        OnStop(exception);

    public new async Task Start(CancellationToken cancellationToken = default) =>
        await base.Start(cancellationToken);

    public new async Task Stop() => await base.Stop();

    public new async Task Watch(CancellationToken cancellationToken = default) =>
        await base.Watch(cancellationToken);
}
