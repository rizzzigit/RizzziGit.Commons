namespace RizzziGit.Commons.Net.HybridWebSocket;

public partial class HybridWebSocket
{
    private void HandleResponse(uint id, Payload payload)
    {
        if (PendingOutgoingRequests.Remove(id, out TaskCompletionSource<Payload>? value))
        {
            value.SetResult(payload);
        }
    }
}
