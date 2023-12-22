namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  private async Task SendClose(bool isAbrupt)
  {
    if ((StateInt == STATE_LOCAL_CLOSING) || (StateInt != STATE_OPEN && StateInt != STATE_REMOTE_CLOSING))
    {
      return;
    }

    await SendData(CompositeBuffer.Concat(
      CompositeBuffer.From(DATA_CLOSE),
      CompositeBuffer.From((byte)(isAbrupt ? 0x01 : 0x00))
    ));

    StateInt = StateInt == STATE_REMOTE_CLOSING ? STATE_CLOSED : STATE_LOCAL_CLOSING;
  }
}
