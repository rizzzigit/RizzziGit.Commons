using System.Collections.Concurrent;

namespace RizzziGit.Commons.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private readonly ConcurrentDictionary<uint, TaskCompletionSource<(uint responseCode, CompositeBuffer responsePayload)>> PendingOutgoingRequests = [];

  public bool CanRequest => StateInt == STATE_OPEN;
  public async Task<(uint responseCode, CompositeBuffer responsePayload)> Request(uint requestCode, CompositeBuffer requestPayload, CancellationToken cancellationToken)
  {
    if (!CanRequest)
    {
      throw new InvalidOperationException("Invalid state to send request.");
    }

    uint id;
    TaskCompletionSource<(uint responseCode, CompositeBuffer responsePayload)> source = new();

    lock (PendingOutgoingRequests)
    {
      do
      {
        id = (uint)Random.Shared.Next();
      }
      while (!PendingOutgoingRequests.TryAdd(id, source));
    }

    CancellationTokenRegistration? cancellationTokenRegistration = null;
    cancellationTokenRegistration = cancellationToken.Register(() =>
    {
      if (StateInt == STATE_OPEN)
      {
        SendCancelRequest(id, source);
      }
      cancellationTokenRegistration?.Unregister();
    });

    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      await SendRequest(id, requestCode, requestPayload);
      return await source.Task;
    }
    finally
    {
      cancellationTokenRegistration?.Unregister();

      lock (PendingOutgoingRequests)
      {
        PendingOutgoingRequests.Remove(id, out var _);
      }
    }
  }
}
