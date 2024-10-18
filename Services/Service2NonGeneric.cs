namespace RizzziGit.Commons.Services;

using Logging;

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

    public new async Task Watch(CancellationToken cancellationToken = default) =>
        await base.Watch(cancellationToken);
}
