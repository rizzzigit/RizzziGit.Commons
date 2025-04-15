using System.Threading.Tasks;
using RizzziGit.Commons.Threading;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    const string LOGGING_SCOPE_POST_RUN_AWAITER = "Post-Run Awaiter";

    private sealed record PostRunEntry(string? Description, Task Task);

    private sealed partial class ServiceInstance
    {
        public required List<PostRunEntry> PostRunWaitList;
        public required SemaphoreSlim PostRunWaitListSemaphore;
    }

    protected Task WaitBeforeStopping(
        Func<Task> function,
        string? description = null,
        CancellationToken cancellationToken = default
    )
    {
        Task task = function();

        WaitTaskBeforeStopping(task, description, cancellationToken);
        return task;
    }

    protected Task WaitBeforeStopping(
        Func<CancellationToken, Task> function,
        string? description = null,
        CancellationToken cancellationToken = default
    ) => WaitBeforeStopping(() => function(cancellationToken), description, cancellationToken);

    protected async void WaitTaskBeforeStopping(
        Task task,
        string? description = null,
        CancellationToken cancellationToken = default
    )
    {
        lock (task)
        {
            if (task.IsCompleted)
            {
                if (task.Exception != null)
                {
                    Error(task.Exception, [LOGGING_SCOPE_POST_RUN_AWAITER]);
                }

                return;
            }
        }

        PostRunEntry postRunEntry = new(description, task);
        CancellationToken serviceCancellationToken = GetServiceCancellationToken();

        try
        {
            InternalContext.PostRunWaitListSemaphore.WithSemaphore(
                () => InternalContext.PostRunWaitList.Add(postRunEntry)
            );
            Debug($"Added: {description ?? "Unknown task"}", [LOGGING_SCOPE_POST_RUN_AWAITER]);

            try
            {
                await task.WaitAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                if (
                    exception is OperationCanceledException operationCanceledException
                    && (
                        operationCanceledException.CancellationToken == cancellationToken
                        || operationCanceledException.CancellationToken == serviceCancellationToken
                    )
                )
                {
                    if (operationCanceledException.CancellationToken == cancellationToken)
                    {
                        Debug(
                            $"Cancelled: {description ?? "Unknown task"}",
                            [LOGGING_SCOPE_POST_RUN_AWAITER]
                        );
                    }

                    return;
                }

                Error(exception, [LOGGING_SCOPE_POST_RUN_AWAITER]);
            }
        }
        finally
        {
            InternalContext.PostRunWaitListSemaphore.WithSemaphore(
                () => InternalContext.PostRunWaitList.Remove(postRunEntry)
            );
            Debug($"Remove: {description ?? "Unknown task"}", [LOGGING_SCOPE_POST_RUN_AWAITER]);
        }
    }
}
