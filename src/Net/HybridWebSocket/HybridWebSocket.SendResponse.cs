namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private Task SendResponse(uint id, uint responseCode, CompositeBuffer responsePayload) => SendData(CompositeBuffer.Concat(
    CompositeBuffer.From(DATA_RESPONSE),
    CompositeBuffer.From(id),
    CompositeBuffer.From(responseCode),
    responsePayload
  ));
}
