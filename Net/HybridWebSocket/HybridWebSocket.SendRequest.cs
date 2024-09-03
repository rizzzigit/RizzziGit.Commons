namespace RizzziGit.Commons.Net;

using Memory;

public partial class HybridWebSocket
{
  private async Task SendRequest(uint id, Payload payload)
  {
    await SendData(CompositeBuffer.Concat(
      CompositeBuffer.From(DATA_REQUEST),
      CompositeBuffer.From(id),
      CompositeBuffer.From(payload.Code),
      payload.Buffer
    ));
  }
}
