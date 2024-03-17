namespace RizzziGit.Commons.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private readonly Dictionary<uint, CancellationTokenSource> IncomingRequestCancellationTokens = [];

  private async Task HandleRequest(uint id, uint requestCode, CompositeBuffer requestPayload, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    CancellationTokenSource cancellationTokenSource = new();
    CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
      cancellationToken,
      cancellationTokenSource.Token
    );

    try
    {
      try
      {
        lock (IncomingRequestCancellationTokens)
        {
          IncomingRequestCancellationTokens.Add(id, cancellationTokenSource);
        }

        linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
        (uint responseCode, CompositeBuffer responsePayload) = await OnRequest(new(requestCode, requestPayload), linkedCancellationTokenSource.Token);

        linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
        await SendResponse(id, responseCode, responsePayload);
      }
      catch (OperationCanceledException)
      {
        try { await SendCancelResponse(id); } catch { }
      }
      catch (Exception exception)
      {
        try { await SendErrorResponse(id, exception.GetType().FullName ?? exception.GetType().Name, exception.Message); } catch { }
      }
    }
    finally
    {
      linkedCancellationTokenSource.Dispose();
      cancellationTokenSource.Dispose();

      lock (IncomingRequestCancellationTokens)
      {
        IncomingRequestCancellationTokens.Remove(id);
      }
    }
  }
}
