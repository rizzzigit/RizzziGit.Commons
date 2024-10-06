namespace RizzziGit.Commons.Net.HybridWebSocket2;

public sealed partial class HybridWebSocket
{
    protected sealed override async Task OnStop(
        HybridWebSocketContext context,
        Exception? exception
    )
    {
        foreach (var (key, value) in context.IncomingMessages)
        {
            await value.Abort(exception);
        }

        foreach (var (key, value) in context.IncomingResponses)
        {
            await value.Abort(exception);
        }

        foreach (var (key, value) in context.IncomingRequests)
        {
            await value.Abort(exception);
        }

        foreach (var (key, value) in context.IncomingResponseErrors)
        {
            await value.Abort(exception);
        }

        foreach (var (key, value) in context.IncomingPongs)
        {
            value.SetException(exception ?? new Exception("Shutting down"));
        }

        context.IncomingShutdownCompletes?.SetException(
            exception ?? new Exception("Shutting down")
        );

        Context.Results.Dispose(exception);
    }
}
