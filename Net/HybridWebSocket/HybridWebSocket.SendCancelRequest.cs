namespace RizzziGit.Commons.Net;

using Memory;

public partial class HybridWebSocket
{
  private async void SendCancelRequest(uint id, TaskCompletionSource<Payload> source)
  {
    try
    {
      await SendData(CompositeBuffer.Concat(
        CompositeBuffer.From(DATA_CANCEL_REQUEST),
        CompositeBuffer.From(id)
      ));
    }
    catch (Exception exception)
    {
      source.TrySetException(exception);
    }
  }
}
