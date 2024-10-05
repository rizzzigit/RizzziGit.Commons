using System.Collections.Concurrent;

namespace RizzziGit.Commons.Net.HybridWebSocket;

public partial class HybridWebSocket
{
    private readonly ConcurrentDictionary<
        uint,
        TaskCompletionSource<Payload>
    > PendingOutgoingRequests = [];

    public bool CanRequest => StateInt == STATE_OPEN;

    public async Task<Payload> Request(Payload payload, CancellationToken cancellationToken)
    {
        if (!CanRequest)
        {
            throw new InvalidOperationException("Invalid state to send request.");
        }

        uint id;
        TaskCompletionSource<Payload> source = new();

        lock (PendingOutgoingRequests)
        {
            do
            {
                id = (uint)Random.Shared.Next();
            } while (!PendingOutgoingRequests.TryAdd(id, source));
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
            await SendRequest(id, payload);
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
