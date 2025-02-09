using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    private async Task RunInternal(
        ServiceInstance serviceContext,
        CancellationToken startupCancellationToken,
        TaskCompletionSource startupTaskCompletionSource,
        CancellationTokenSource serviceCancellationTokenSource
    )
    {
        SetState(serviceContext, ServiceState.StartingUp);

        void logError(Exception exception)
        {
            lastException = ExceptionDispatchInfo.Capture(exception);
            ExceptionThrown?.Invoke(this, exception);
            Fatal(exception.ToPrintable(), "Fatal Error");
        }

        try
        {
            serviceContext.Context = new(
                await StartInternal(startupCancellationToken, serviceCancellationTokenSource.Token)
            );

            SetState(serviceContext, ServiceState.Running);
            startupTaskCompletionSource.SetResult();
        }
        catch (Exception exception)
        {
            logError(exception);

            SetState(serviceContext, ServiceState.CrashingDown);
            SetState(serviceContext, ServiceState.Crashed);

            startupTaskCompletionSource.SetException(exception);
            throw;
        }

        try
        {
            await RunInternal(serviceContext.Context!.Value!, serviceCancellationTokenSource);

            SetState(serviceContext, ServiceState.ShuttingDown);
        }
        catch (Exception exception)
        {
            logError(exception);

            SetState(serviceContext, ServiceState.CrashingDown);

            await StopInternal(
                serviceContext.Context!.Value!,
                ExceptionDispatchInfo.Capture(exception)
            );

            SetState(serviceContext, ServiceState.Crashed);
            throw;
        }

        try
        {
            await StopInternal(serviceContext.Context!.Value!, null);

            SetState(serviceContext, ServiceState.NotRunning);
        }
        catch (Exception exception)
        {
            logError(exception);

            SetState(serviceContext, ServiceState.CrashingDown);
            SetState(serviceContext, ServiceState.Crashed);
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

        return await OnStart(startupLinkedCancellationTokenSource.Token, serviceCancellationToken);
    }

    private async Task RunInternal(
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

            await foreach (Task task in Task.WhenEach([.. WaitTasksBeforeStopping]))
            {
                try
                {
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

            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Throw(exceptions[0]);
            }
            else if (exceptions.Count > 1)
            {
                throw new AggregateException(exceptions);
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

    private async Task StopInternal(C context, ExceptionDispatchInfo? exception)
    {
        try
        {
            await OnStop(context, exception);
        }
        catch (Exception e)
        {
            if (exception != null)
            {
                throw new AggregateException(exception.SourceException, e);
            }

            throw;
        }
    }
}
