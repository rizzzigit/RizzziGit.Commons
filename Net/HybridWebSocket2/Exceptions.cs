namespace RizzziGit.Commons.Net.HybridWebSocket2;

using Memory;

public class HybridWebSocketException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class UnexpectedPacketException(
    IHybridWebSocketPacket packet,
    Exception? innerException = null
) : HybridWebSocketException($"Unexpected packet: {packet.GetType().Name}", innerException);

public class AbruptShutdownException(
    ShutdownChannelAbruptReason reason,
    Exception? innerException = null
) : HybridWebSocketException("Abrupt shutdown", innerException)
{
    public ShutdownChannelAbruptReason Reason => reason;
}

public sealed class ResponseException(HybridWebSocket.Stream stream, Exception? exception = null)
    : HybridWebSocketException("Server responded with an error", exception)
{
    public HybridWebSocket.Stream Stream => stream;
}
