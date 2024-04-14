namespace RizzziGit.Commons.Net;

public partial class HybridWebSocket
{
  private void HandleCancelResponse(uint id)
  {
    if (PendingOutgoingRequests.Remove(id, out TaskCompletionSource<Payload>? value))
    {
      value.SetCanceled();
    }
  }
}
