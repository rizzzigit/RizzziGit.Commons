namespace RizzziGit.Commons.Utilities;

public static class CancellationTokenExtensions
{
    public static CancellationTokenSource Link(
        this CancellationToken cancellationToken,
        params CancellationToken?[] cancellationTokens
    ) =>
        CancellationTokenSource.CreateLinkedTokenSource(
            [
                cancellationToken,
                .. cancellationTokens.Where(x => x is not null).Select(x => (CancellationToken)x!)
            ]
        );

    public static async Task GetTask(
        this CancellationToken cancellationToken,
        bool throwException = false
    )
    {
        try
        {
            await Task.Delay(-1, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (throwException)
            {
                throw;
            }
        }
    }
}
