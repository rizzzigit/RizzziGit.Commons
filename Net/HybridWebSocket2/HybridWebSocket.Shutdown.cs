namespace RizzziGit.Commons.Net.HybridWebSocket2;

public sealed partial class HybridWebSocket
{
    public async Task Shutdown()
    {
        TaskCompletionSource taskCompletionSource;

        lock (Context)
        {
            taskCompletionSource = new();
            Context.IsShuttingDown = true;
            Context.IncomingShutdownCompletes = taskCompletionSource;
        }

        await Send(new ShutdownPacket() { });
        await taskCompletionSource.Task;
    }
}
