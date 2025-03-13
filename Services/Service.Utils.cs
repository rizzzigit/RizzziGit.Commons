namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    public async Task RunInconsequential(
        Func<CancellationToken, Task> run,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await run(cancellationToken);
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

            Error(exception);
        }
    }

    public Task RunInconsequential(Func<Task> run, CancellationToken cancellationToken = default) =>
        RunInconsequential((_) => run(), cancellationToken);

    public void RunInconsequential(
        Action<CancellationToken> run,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            run(cancellationToken);
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

            Error(exception);
        }
    }

    public void RunInconsequential(Action run, CancellationToken cancellationToken = default) =>
        RunInconsequential((_) => run(), cancellationToken);

    protected async Task StartChildService(IService service, CancellationToken cancellationToken)
    {
        CancellationToken serviceCancellationToken = GetServiceCancellationToken();

        {
            using CancellationTokenSource linkedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    serviceCancellationToken,
                    cancellationToken
                );

            await service.Start(linkedCancellationTokenSource.Token);
        }

        _ = WaitBeforeStopping(
            service.Stop,
            $"{service.Name} Service",
            CancellationToken.None
        );
    }
}
