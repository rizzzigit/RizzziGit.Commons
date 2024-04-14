namespace RizzziGit.Commons.Net;

using Memory;

public partial class HybridWebSocket
{
  private async Task SendMessage(CompositeBuffer message)
  {
    await SendData(CompositeBuffer.Concat(
      CompositeBuffer.From(DATA_MESSAGE),
      message
    ));
  }
}
