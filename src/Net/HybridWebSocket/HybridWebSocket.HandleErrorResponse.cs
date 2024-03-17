namespace RizzziGit.Commons.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private void HandleErrorResponse(uint id, string name, string message)
  {
    if (PendingOutgoingRequests.Remove(id, out TaskCompletionSource<(uint responseCode, CompositeBuffer responsePayload)>? value))
    {
      value.SetException(new Exception($"{name}: {message}"));
    }
  }
}
