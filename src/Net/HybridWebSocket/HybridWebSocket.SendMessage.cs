namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private async Task SendMessage(CompositeBuffer message)
  {
    await SendData(CompositeBuffer.Concat(
      CompositeBuffer.From(DATA_MESSAGE),
      message
    ));
  }
}
