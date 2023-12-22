namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  public bool CanMessage => (StateInt == STATE_OPEN) || (StateInt == STATE_REMOTE_CLOSING);
  public async Task Message(CompositeBuffer message)
  {
    if (!CanMessage)
    {
      throw new InvalidOperationException("Invalid state to send message.");
    }

    await SendMessage(message);
  }
}
