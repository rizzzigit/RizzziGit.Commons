namespace RizzziGit.Framework.Net;

using System.Net.WebSockets;
using Memory;

public abstract partial class HybridWebSocket
{
  public event Action<byte>? OnSend;

  private async Task SendData(CompositeBuffer data)
  {
    if ((StateInt == STATE_LOCAL_CLOSING) || (StateInt != STATE_OPEN && StateInt != STATE_REMOTE_CLOSING))
    {
      throw new InvalidOperationException("Invalid state to send data.");
    }

    OnSend?.Invoke(data[0]);

    await WebSocket.SendAsync(data.ToByteArray(), WebSocketMessageType.Binary, true, RequireCancellationToken());
  }
}
