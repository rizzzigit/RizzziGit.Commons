namespace RizzziGit.Commons.Net;

using System.Net.WebSockets;
using Memory;

public partial class HybridWebSocket
{
  public event Action<byte>? OnSend;

  private async Task SendData(CompositeBuffer data)
  {
    if ((StateInt == STATE_LOCAL_CLOSING) || (StateInt != STATE_OPEN && StateInt != STATE_REMOTE_CLOSING))
    {
      throw new InvalidOperationException("Invalid state to send data.");
    }

    OnSend?.Invoke(data[0]);

    for (long offset = 0; offset < data.Length; offset += 4096)
    {
      await WebSocket.SendAsync(data.Slice(offset, Math.Min(offset + 4096, data.Length)).ToByteArray(), WebSocketMessageType.Binary, false, RequireCancellationToken());
    }

    await WebSocket.SendAsync([], WebSocketMessageType.Binary, true, RequireCancellationToken());
  }
}
