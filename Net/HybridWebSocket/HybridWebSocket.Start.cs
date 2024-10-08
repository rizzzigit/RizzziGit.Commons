namespace RizzziGit.Commons.Net.HybridWebSocket;

public partial class HybridWebSocket
{
    private (Task task, CancellationToken cancellationToken)? Context;

    private CancellationToken RequireCancellationToken()
    {
        lock (this)
        {
            return Context?.cancellationToken
                ?? throw new InvalidOperationException("Not connected.");
        }
    }

    public int StateInt { get; private set; } = STATE_CLOSED;
    public HybridWebSocketState State =>
        (HybridWebSocketState)Enum.ToObject(typeof(HybridWebSocketState), StateInt);

    protected async Task Start(CancellationToken cancellationToken)
    {
        if (Context != null)
        {
            throw new InvalidOperationException("Receive loop is already running.");
        }

        try
        {
            StateInt = STATE_OPEN;

            try
            {
                try
                {
                    Task task;
                    lock (this)
                    {
                        Context = (task = RunStart(cancellationToken), cancellationToken);
                    }
                    await task;
                }
                catch
                {
                    if (StateInt == STATE_REMOTE_CLOSING || StateInt == STATE_ABORTED)
                    {
                        try
                        {
                            await SendClose(true);
                        }
                        catch { }
                    }

                    StateInt = STATE_ABORTED;
                    throw;
                }

                if (StateInt == STATE_REMOTE_CLOSING || StateInt == STATE_ABORTED)
                {
                    try
                    {
                        await SendClose(false);
                    }
                    catch { }
                }

                StateInt = STATE_CLOSED;
            }
            finally
            {
                lock (this)
                {
                    Context = null;
                }
            }
        }
        catch (Exception exception)
        {
            lock (PendingOutgoingRequests)
            {
                foreach (
                    KeyValuePair<
                        uint,
                        TaskCompletionSource<Payload>
                    > PendingOutgoingRequests in PendingOutgoingRequests
                )
                {
                    PendingOutgoingRequests.Value.TrySetException(exception);
                }
            }

            PendingOutgoingRequests.Clear();
            throw;
        }
        finally
        {
            lock (PendingOutgoingRequests)
            {
                foreach (
                    KeyValuePair<
                        uint,
                        TaskCompletionSource<Payload>
                    > PendingOutgoingRequests in PendingOutgoingRequests
                )
                {
                    PendingOutgoingRequests.Value.TrySetCanceled();
                }
            }

            PendingOutgoingRequests.Clear();
        }
    }
}
