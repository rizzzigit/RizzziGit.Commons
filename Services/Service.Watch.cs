namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    public async Task Watch(CancellationToken cancellationToken = default)
    {
        lastException?.Throw();

        Task task;

        lock (this)
        {
            task = instance?.Task ?? Task.CompletedTask;
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
