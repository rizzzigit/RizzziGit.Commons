using System.Net.WebSockets;

namespace RizzziGit.Commons.Net.HybridWebSocket2;

using Collections;

public sealed partial class HybridWebSocket
{
    protected sealed override Task<HybridWebSocketContext> OnStart(
        CancellationToken cancellationToken
    ) => isServer ? InitServer(cancellationToken) : InitClient(cancellationToken);

    private async Task<HybridWebSocketContext> InitClient(CancellationToken cancellationToken)
    {
        Info("Initialization handshake has started.");

        while (true)
        {
            Info("Sending initialization request...");
            await Send(new InitRequestPacket(), cancellationToken);

            Info("Initialization request sent. Waiting for response...");
            switch (
                await Receive(
                    () =>

                        [
                            HybridWebSocketPacketType.InitResponse,
                            HybridWebSocketPacketType.ShutdownAbrupt
                        ],
                    cancellationToken
                )
            )
            {
                case InitResponsePacket:
                    Info("Received channel response.");
                    return new()
                    {
                        IsShuttingDown = false,
                        IncomingRequests = [],
                        IncomingResponses = [],
                        IncomingResponseErrors = [],
                        IncomingMessages = [],
                        IncomingPongs = [],
                        IncomingShutdownCompletes = null,
                        NextMessageId = 0,
                        NextRequestId = 0,
                        Results = new(),
                        Exceptions = new()
                    };

                case ShutdownAbruptPacket shutdownChannelAbruptPacket:
                    throw new AbruptShutdownException(shutdownChannelAbruptPacket.Reason);
            }
        }
    }

    private async Task<HybridWebSocketContext> InitServer(CancellationToken cancellationToken)
    {
        Info("Waiting for channel ID...");

        while (true)
        {
            switch (
                await Receive(
                    () =>

                        [
                            HybridWebSocketPacketType.InitRequest,
                            HybridWebSocketPacketType.ShutdownAbrupt
                        ],
                    CancellationToken.None
                )
            )
            {
                case InitRequestPacket:
                {
                    Info("Sending channel response...");
                    await Send(new InitResponsePacket(), cancellationToken);

                    return new()
                    {
                        IsShuttingDown = false,
                        IncomingRequests = [],
                        IncomingResponses = [],
                        IncomingResponseErrors = [],
                        IncomingMessages = [],
                        IncomingPongs = [],
                        IncomingShutdownCompletes = null,
                        NextMessageId = 0,
                        NextRequestId = 0,
                        Results = new(),
                        Exceptions = new()
                    };
                }

                case ShutdownAbruptPacket packet:
                    throw new AbruptShutdownException(packet.Reason);
            }
        }
    }
}
