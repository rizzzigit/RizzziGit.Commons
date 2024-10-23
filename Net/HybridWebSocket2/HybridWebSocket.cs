using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RizzziGit.Commons.Net.HybridWebSocket2;

using System.Runtime.ExceptionServices;
using Collections;
using Logging;
using Memory;
using Services;
using Utilities;

public sealed class HybridWebSocketContext
{
    public required ConcurrentDictionary<ulong, HybridWebSocket.Stream> IncomingRequests;
    public required ConcurrentDictionary<ulong, HybridWebSocket.Stream> IncomingResponses;
    public required ConcurrentDictionary<ulong, HybridWebSocket.Stream> IncomingResponseErrors;
    public required ConcurrentDictionary<ulong, HybridWebSocket.Stream> IncomingMessages;
    public required ConcurrentDictionary<ulong, TaskCompletionSource> IncomingPongs;
    public required TaskCompletionSource? IncomingShutdownCompletes;

    public required WaitQueue<HybridWebSocketResult> Results;
    public required List<Exception> Exceptions;

    public required bool IsShuttingDown;

    public required ulong NextRequestId = 0;
    public required ulong NextMessageId = 0;
}

public enum HybridWebSocketMode : byte
{
    Client,
    Server
}

public sealed partial class HybridWebSocket(
    System.Net.WebSockets.WebSocket webSocket,
    bool isServer,
    string name,
    Logger? downstream = null
) : Service2<HybridWebSocketContext>(name, downstream)
{
    public HybridWebSocket(System.Net.WebSockets.WebSocket webSocket, bool isServer, string name, IService2 downstream)
        : this(webSocket, isServer, name, downstream.Logger) { }

    private readonly System.Net.WebSockets.WebSocket webSocket = webSocket;
    private readonly bool isServer = isServer;

    private async ValueTask<IHybridWebSocketPacket?> Receive(
        Func<HybridWebSocketPacketType?[]>? expect,
        CancellationToken cancellationToken = default
    )
    {
        async ValueTask<IHybridWebSocketPacket?> receive()
        {
            while (true)
            {
                CompositeBuffer bytes = [];

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    byte[] buffer = new byte[4096];

                    WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(
                        buffer,
                        cancellationToken
                    );

                    bytes.Append(buffer, 0, receiveResult.Count);

                    if (receiveResult.CloseStatus != null)
                    {
                        return null;
                    }

                    if (receiveResult.EndOfMessage)
                    {
                        break;
                    }
                }

                IHybridWebSocketPacket packet = Deserialize(bytes);
                HybridWebSocketPacketType type = (HybridWebSocketPacketType)bytes[0];
                if (expect is not null && !expect().Where((e) => e is not null).Contains(type))
                {
                    throw new UnexpectedPacketException(packet);
                }

                Debug($"{packet.GetType().Name} {bytes.ToHexString()}", "Received Packet");

                return packet;
            }
        }

        try
        {
            return await receive();
        }
        catch (Exception exception)
        {
            try
            {
                if (webSocket.CloseStatus != null)
                {
                    await Send(
                        new ShutdownAbruptPacket()
                        {
                            Reason =
                                exception is UnexpectedPacketException
                                    ? ShutdownChannelAbruptReason.UnexpectedPacket
                                    : ShutdownChannelAbruptReason.InternalError
                        },
                        CancellationToken.None
                    );
                }
            }
            catch (Exception exception2)
            {
                throw new AggregateException(exception, exception2);
            }

            throw;
        }
    }

    private async Task Send(
        IHybridWebSocketPacket hybridWebSocketPacket,
        CancellationToken cancellationToken = default
    )
    {
        async Task send(
            IHybridWebSocketPacket hybridWebSocketPacket,
            CancellationToken cancellationToken = default
        )
        {
            CompositeBuffer bytes = Serialize(hybridWebSocketPacket);

            Debug($"{hybridWebSocketPacket.GetType().Name} {bytes.ToHexString()}", "Sent Packet");
            // Console.WriteLine(
            //     ExceptionDispatchInfo.SetCurrentStackTrace(new Exception()).StackTrace
            // );
            await webSocket.SendAsync(
                bytes.ToByteArray(),
                WebSocketMessageType.Binary,
                true,
                cancellationToken
            );
        }

        try
        {
            await send(hybridWebSocketPacket, cancellationToken);
        }
        catch (Exception exception)
        {
            if (webSocket.CloseStatus != null)
            {
                try
                {
                    await send(
                        new ShutdownAbruptPacket()
                        {
                            Reason = ShutdownChannelAbruptReason.InternalError
                        },
                        CancellationToken.None
                    );
                }
                catch (Exception exception2)
                {
                    throw new AggregateException(exception, exception2);
                }
            }

            throw;
        }
    }

    public delegate Task OnStartDelegate(CancellationToken cancellationToken = default);

    public new OnStartDelegate Start => base.Start;
}
