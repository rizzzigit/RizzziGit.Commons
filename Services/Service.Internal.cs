using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Threading;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    private async Task RunInternal(
        ServiceInstance instance,
        TaskCompletionSource startupTaskCompletionSource,
        CancellationTokenSource serviceCancellationTokenSource,
        CancellationToken startupCancellationToken
    )
    {
        SetState(instance, ServiceState.StartingUp);

        void logError(Exception exception)
        {
            lastException = ExceptionDispatchInfo.Capture(exception);
            ExceptionThrown?.Invoke(this, exception);
            Fatal(exception.ToPrintable(), "Fatal Error");
        }

        try
        {
            instance.Context = new(
                await StartInternal(startupCancellationToken, serviceCancellationTokenSource.Token)
            );

            SetState(instance, ServiceState.Running);
            startupTaskCompletionSource.SetResult();
        }
        catch (Exception exception)
        {
            serviceCancellationTokenSource.Cancel();

            logError(exception);

            SetState(instance, ServiceState.CrashingDown);

            {
                List<Exception> stopExceptions = [];

                while (true)
                {
                    IService[] services =
                        instance.ChildSeviceListSemaphore.WithSemaphore<IService[]>(
                            () => [.. instance.ChildSeviceList]
                        );

                    if (services.Length == 0)
                    {
                        break;
                    }

                    await StopChildServices(services, stopExceptions);
                }

                if (stopExceptions.Count != 0)
                {
                    AggregateException aggregateException = new([exception, .. stopExceptions]);

                    startupTaskCompletionSource.SetException(aggregateException);
                    throw aggregateException;
                }
            }

            SetState(instance, ServiceState.Crashed);

            startupTaskCompletionSource.SetException(exception);
            throw;
        }

        try
        {
            await RunInternal(instance, instance.Context!.Value!, serviceCancellationTokenSource);

            SetState(instance, ServiceState.ShuttingDown);
        }
        catch (Exception exception)
        {
            logError(exception);

            SetState(instance, ServiceState.CrashingDown);

            await StopInternal(
                instance,
                instance.Context!.Value!,
                ExceptionDispatchInfo.Capture(exception)
            );

            SetState(instance, ServiceState.Crashed);
            throw;
        }

        try
        {
            await StopInternal(instance, instance.Context!.Value!, null);

            SetState(instance, ServiceState.NotRunning);
        }
        catch (Exception exception)
        {
            logError(exception);

            SetState(instance, ServiceState.CrashingDown);
            SetState(instance, ServiceState.Crashed);
            throw;
        }
    }

    private async Task<C> StartInternal(
        CancellationToken startupCancellationToken,
        CancellationToken serviceCancellationToken
    )
    {
        using CancellationTokenSource startupLinkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                startupCancellationToken,
                serviceCancellationToken
            );

        return await OnStart(startupLinkedCancellationTokenSource.Token);
    }

    private async Task RunInternal(
        ServiceInstance instance,
        C context,
        CancellationTokenSource serviceCancellationTokenSource
    )
    {
        CancellationToken cancellationToken = serviceCancellationTokenSource.Token;

        try
        {
            List<Exception> exceptions = [];

            try
            {
                using (serviceCancellationTokenSource)
                {
                    await OnRun(context, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }

            while (true)
            {
                PostRunEntry[] tasks = instance.PostRunWaitListSemaphore.WithSemaphore(() =>
                {
                    PostRunEntry[] tasks = [.. instance.PostRunWaitList.Reverse<PostRunEntry>()];

                    return tasks;
                });

                if (tasks.Length == 0)
                {
                    break;
                }

                foreach ((string? description, Task task) in tasks)
                {
                    try
                    {
                        Debug(
                            $"Waiting for {description ?? "a task"} to complete...",
                            LOGGING_SCOPE_POST_RUN_AWAITER
                        );
                        await task;
                    }
                    catch (Exception exception)
                    {
                        if (
                            exception is OperationCanceledException operationCanceledException
                            && (operationCanceledException.CancellationToken == cancellationToken)
                        )
                        {
                            continue;
                        }

                        exceptions.Add(exception);
                    }
                }
            }
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

    private async Task StopInternal(
        ServiceInstance instance,
        C context,
        ExceptionDispatchInfo? exception
    )
    {
        List<Exception> exceptions = [];

        if (exception is not null)
        {
            exceptions.Add(exception.SourceException);
        }

        while (true)
        {
            IService[] services = instance.ChildSeviceListSemaphore.WithSemaphore<IService[]>(
                () => [.. instance.ChildSeviceList]
            );

            if (services.Length == 0)
            {
                break;
            }

            await StopChildServices(services, exceptions);
        }

        try
        {
            await OnStop(
                context,
                exceptions.Count == 0
                    ? null
                    : ExceptionDispatchInfo.Capture(
                        exceptions.Count == 1 ? exceptions[0] : new AggregateException(exceptions)
                    )
            );
        }
        catch (Exception e)
        {
            exceptions.Add(e);
        }

        if (exceptions.Count == 0)
        {
            return;
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Throw(exceptions.First());
        }

        throw new AggregateException(exceptions);
    }

    private async Task StopChildServices(IService[] services, List<Exception> exceptions)
    {
        foreach (IService service in services.AsEnumerable().Reverse())
        {
            try
            {
                Debug($"Waiting for {service.Name} service to stop...", "Child Services");

                await service.Stop();
            }
            catch (Exception serviceException)
            {
                exceptions.Add(serviceException);
            }
        }
    }
}
