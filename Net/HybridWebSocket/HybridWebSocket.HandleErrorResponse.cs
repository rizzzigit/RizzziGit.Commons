namespace RizzziGit.Commons.Net.HybridWebSocket;

public partial class HybridWebSocket
{
    private void HandleErrorResponse(uint id, string name, string message)
    {
        if (PendingOutgoingRequests.Remove(id, out TaskCompletionSource<Payload>? value))
        {
            value.SetException(new Exception($"{name}: {message}"));
        }
    }
}
