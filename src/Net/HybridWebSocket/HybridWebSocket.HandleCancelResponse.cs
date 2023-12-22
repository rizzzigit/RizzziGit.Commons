namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private void HandleCancelResponse(uint id)
  {
    if (PendingOutgoingRequests.Remove(id, out TaskCompletionSource<(uint responseCode, CompositeBuffer responsePayload)>? value))
    {
      value.SetCanceled();
    }
  }
}
