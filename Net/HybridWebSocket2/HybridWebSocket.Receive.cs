namespace RizzziGit.Commons.Net.HybridWebSocket2;

public sealed partial class HybridWebSocket
{
    public async Task<HybridWebSocketResult> Receive(CancellationToken cancellationToken)
    {
        using CancellationTokenSource cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);

        return await Context.Results.Dequeue(cancellationTokenSource.Token);
    }
}
