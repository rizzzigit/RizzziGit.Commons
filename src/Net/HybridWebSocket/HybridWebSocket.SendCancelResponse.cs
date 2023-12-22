namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private Task SendCancelResponse(uint id) => SendData(CompositeBuffer.Concat(
    CompositeBuffer.From(DATA_CANCEL_RESPONSE),
    CompositeBuffer.From(id)
  ));
}
