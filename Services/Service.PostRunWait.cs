using System.Threading.Tasks;
using RizzziGit.Commons.Threading;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    private sealed partial class ServiceInstance
    {
        public required List<Task> PostRunWaitList;
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

    protected async void WaitTaskBeforeStopping(
        Task task,
        string? description = null,
        CancellationToken cancellationToken = default
    )
    {
        string scope = "Post-Run Awaiter";
        try
        {
            try
            {
                InternalContext.PostRunWaitListSemaphore.WithSemaphore(
                    () => InternalContext.PostRunWaitList.Add(task)
                );

                Debug($"Added to post-run waiting list: {description ?? "Unknown task"}", scope);
                await task.WaitAsync(cancellationToken);
                Debug($"Task Completed: {description ?? "Unknown task"}", scope);
            }
            catch (Exception exception)
            {
                if (
                    exception is OperationCanceledException operationCanceledException
                    && operationCanceledException.CancellationToken == cancellationToken
                )
                {
                    Debug($"Waiting cancelled for task: {description ?? "Unknown task"}", scope);
                    return;
                }

                Error(exception, scope);
            }
        }
        finally
        {
            InternalContext.PostRunWaitListSemaphore.WithSemaphore(
                () => InternalContext.PostRunWaitList.Remove(task)
            );
        }
    }
}
