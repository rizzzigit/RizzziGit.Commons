namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private void HandleResponse(uint id, uint responseCode, CompositeBuffer responsePayload)
  {
    if (PendingOutgoingRequests.Remove(id, out TaskCompletionSource<(uint responseCode, CompositeBuffer responsePayload)>? value))
    {
      value.SetResult((responseCode, responsePayload));
    }
  }
}
