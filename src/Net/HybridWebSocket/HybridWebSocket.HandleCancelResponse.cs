namespace RizzziGit.Commons.Net;

public abstract partial class HybridWebSocket
{
  private void HandleCancelResponse(uint id)
  {
    if (PendingOutgoingRequests.Remove(id, out TaskCompletionSource<Payload>? value))
    {
      value.SetCanceled();
    }
  }
}
