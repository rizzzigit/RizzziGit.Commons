namespace RizzziGit.Commons.Services;

public abstract partial class Service2<C>
{
    protected async Task WatchService(
        IService2 service,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Info($"Watching service {service.Name}...", "Watch");
            await service.Watch(cancellationToken);
            Info($"Service {service.Name} has stopped.", "Watch");
        }
        catch (Exception exception)
        {
            if (
                exception is OperationCanceledException operationCanceledException
                && operationCanceledException.CancellationToken == cancellationToken
            )
            {
                Info($"Watching service {service.Name} has been canceled.", "Watch");
                return;
            }

            Info($"Service {service.Name} has stopped due to an exception.", "Watch");
            throw;
        }
    }

    public async Task Watch(CancellationToken cancellationToken = default)
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
}
