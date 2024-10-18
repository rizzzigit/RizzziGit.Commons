namespace RizzziGit.Commons.Services;

public abstract partial class Service2<C>
{
    private async Task RunInternal(
        ServiceInstance serviceContext,
        CancellationToken startupCancellationToken,
        TaskCompletionSource startupTaskCompletionSource,
        CancellationToken cancellationToken
    )
    {
        SetState(serviceContext, Service2State.StartingUp);

        try
        {
            serviceContext.Context = new(
                await StartInternal(
                    startupCancellationToken,
                    startupTaskCompletionSource,
                    cancellationToken
                )
            );
        }
        catch
        {
            SetState(serviceContext, Service2State.CrashingDown);
            SetState(serviceContext, Service2State.Crashed);
            throw;
        }

        SetState(serviceContext, Service2State.Running);
        startupTaskCompletionSource.SetResult();

        try
        {
            await RunInternal(serviceContext.Context!.Value!, cancellationToken);
        }
        catch (Exception exception)
        {
            SetState(serviceContext, Service2State.CrashingDown);

            try
            {
                await StopInternal(serviceContext.Context!.Value!, exception);
            }
            catch (Exception stopException)
            {
                SetState(serviceContext, Service2State.Crashed);
                throw new AggregateException(exception, stopException);
            }

            SetState(serviceContext, Service2State.Crashed);
            throw;
        }

        SetState(serviceContext, Service2State.ShuttingDown);

        try
        {
            await StopInternal(serviceContext.Context!.Value!, null);
        }
        catch
        {
            SetState(serviceContext, Service2State.CrashingDown);
            SetState(serviceContext, Service2State.Crashed);
            throw;
        }

        SetState(serviceContext, Service2State.NotRunning);
    }

    private async Task<C> StartInternal(
        CancellationToken startupCancellationToken,
        TaskCompletionSource startupTaskCompletionSource,
        CancellationToken cancellationToken
    )
    {
        using CancellationTokenSource startupLinkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                startupCancellationToken,
                cancellationToken
            );
        try
        {
            return await OnStart(startupLinkedCancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            startupTaskCompletionSource.SetException(exception);
            throw;
        }
    }

    private async Task RunInternal(C context, CancellationToken cancellationToken)
    {
        try
        {
            await OnRun(context, cancellationToken);
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

    private async Task StopInternal(C context, Exception? exception)
    {
        try
        {
            await OnStop(context, exception);
        }
        catch (Exception e)
        {
            if (exception != null)
            {
                throw new AggregateException(exception, e);
            }

            throw;
        }
    }
}
