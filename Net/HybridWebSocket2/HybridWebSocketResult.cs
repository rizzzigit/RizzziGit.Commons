using System.Collections;
using System.Formats.Tar;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Net.HybridWebSocket2;

using Collections;
using Memory;

public abstract record HybridWebSocketResult
{
    private HybridWebSocketResult() { }

    public sealed record Request(
        HybridWebSocket.Stream RequestStream,
        HybridWebSocket.Stream ResponseStream
    ) : HybridWebSocketResult;

    public sealed record Message(HybridWebSocket.Stream MessageData) : HybridWebSocketResult;
}
