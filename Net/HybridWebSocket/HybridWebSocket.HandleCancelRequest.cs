namespace RizzziGit.Commons.Net.HybridWebSocket;

public partial class HybridWebSocket
{
    private void HandleCancelRequest(uint id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IncomingRequestCancellationTokens.TryGetValue(id, out CancellationTokenSource? value))
        {
            try
            {
                value.Cancel();
            }
            catch { }
        }
    }
}
