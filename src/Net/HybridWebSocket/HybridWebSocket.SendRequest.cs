namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private async Task SendRequest(uint id, uint requestCode, CompositeBuffer requestPayload)
  {
    await SendData(CompositeBuffer.Concat(
      CompositeBuffer.From(DATA_REQUEST),
      CompositeBuffer.From(id),
      CompositeBuffer.From(requestCode),
      requestPayload
    ));
  }
}
