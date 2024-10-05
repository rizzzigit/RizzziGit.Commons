namespace RizzziGit.Commons.Net.HybridWebSocket;

public enum HybridWebSocketState : byte
{
    Closed = HybridWebSocket.STATE_CLOSED,
    Open = HybridWebSocket.STATE_OPEN,
    LocalClosing = HybridWebSocket.STATE_LOCAL_CLOSING,
    RemoteClosing = HybridWebSocket.STATE_REMOTE_CLOSING,
    Aborted = HybridWebSocket.STATE_ABORTED
}

public partial class HybridWebSocket
{
    public const byte STATE_CLOSED = 0;
    public const byte STATE_OPEN = 1;
    public const byte STATE_LOCAL_CLOSING = 2;
    public const byte STATE_REMOTE_CLOSING = 3;
    public const byte STATE_ABORTED = 4;
}
