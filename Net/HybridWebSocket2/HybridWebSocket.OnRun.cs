namespace RizzziGit.Commons.Net.HybridWebSocket2;

using Collections;
using Memory;

public sealed partial class HybridWebSocket
{
    protected sealed override async Task OnRun(
        HybridWebSocketContext context,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Context.Exceptions.Count > 0)
            {
                throw new AggregateException(Context.Exceptions);
            }

            switch (
                await Receive(
                    () => [
                        HybridWebSocketPacketType.RequestBegin,
                        HybridWebSocketPacketType.RequestNext,
                        HybridWebSocketPacketType.RequestDone,
                        HybridWebSocketPacketType.RequestAbort,
                        HybridWebSocketPacketType.MessageBegin,
                        HybridWebSocketPacketType.MessageNext,
                        HybridWebSocketPacketType.MessageDone,
                        HybridWebSocketPacketType.MessageAbort,
                        HybridWebSocketPacketType.ResponseBegin,
                        HybridWebSocketPacketType.ResponseNext,
                        HybridWebSocketPacketType.ResponseDone,
                        HybridWebSocketPacketType.ResponseErrorBegin,
                        HybridWebSocketPacketType.ResponseErrorNext,
                        HybridWebSocketPacketType.ResponseErrorDone,
                        HybridWebSocketPacketType.Ping,
                        HybridWebSocketPacketType.Pong,
                        HybridWebSocketPacketType.ShutdownAbrupt,
                        HybridWebSocketPacketType.Shutdown,
                        Context.IsShuttingDown ? HybridWebSocketPacketType.ShutdownComplete : null
                    ],
                    cancellationToken
                )
            )
            {
                case RequestBeginPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case RequestNextPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case RequestDonePacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case RequestAbortPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case MessageBeginPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case MessageNextPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case MessageDonePacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case MessageAbortPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case IResponseFeedPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case ResponseDonePacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case ResponseErrorBeginPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case ResponseErrorNextPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case ResponseErrorDonePacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case PingPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case PongPacket packet:
                    await Handle(packet, cancellationToken);
                    break;

                case ShutdownPacket:
                    lock (Context)
                    {
                        Context.IsShuttingDown = true;
                    }
                    await Send(new ShutdownCompletePacket(), cancellationToken);
                    return;

                case ShutdownAbruptPacket packet:
                    throw new AbruptShutdownException(packet.Reason);

                case ShutdownCompletePacket:
                {
                    lock (Context)
                    {
                        if (Context.IncomingShutdownCompletes == null)
                        {
                            break;
                        }

                        Context.IncomingShutdownCompletes.SetResult();
                        Context.IncomingShutdownCompletes = null;
                    }
                    return;
                }

                case null:
                    return;
            }
        }
    }

    private Task SendShutdownAbrupt(
        ShutdownChannelAbruptReason reason,
        CancellationToken cancellationToken
    ) => Send(new ShutdownAbruptPacket() { Reason = reason }, cancellationToken);

    private async Task Handle(RequestBeginPacket packet, CancellationToken cancellationToken)
    {
        Stream requestStream = new(cancellationToken);
        Stream responseStream = new(cancellationToken);

        if (!Context.IncomingRequests.TryAdd(packet.RequestId, requestStream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Duplicate request ID.");
        }

        HybridWebSocketResult.Request request = new(requestStream, responseStream);

        await Context.Results.Enqueue(request, cancellationToken);
        await requestStream.Push(packet.RequestData, cancellationToken);

        HandleResponse(packet.RequestId, responseStream, cancellationToken);
    }

    private async void HandleResponse(
        ulong requestId,
        Stream responseStream,
        CancellationToken cancellationToken
    )
    {
        bool first = true;

        while (true)
        {
            CompositeBuffer? entry;
            try
            {
                entry = await responseStream.Shift(cancellationToken);
            }
            catch (StreamRedirectException e)
            {
                if (e.RedirectMode == Stream.RedirectMode.Error)
                {
                    HandleResponseError(requestId, e.Stream, cancellationToken);
                }

                return;
            }

            if (entry == null)
            {
                await Send(new ResponseDonePacket() { RequestId = requestId }, cancellationToken);
                break;
            }
            else if (first)
            {
                await Send(
                    new ResponseBeginPacket()
                    {
                        RequestId = requestId,
                        ResponseData = entry.ToByteArray()
                    },
                    cancellationToken
                );
                first = false;
            }
            else
            {
                await Send(
                    new ResponseNextPacket()
                    {
                        RequestId = requestId,
                        ResponseData = entry.ToByteArray()
                    },
                    cancellationToken
                );
            }
        }
    }

    private async void HandleResponseError(
        ulong requestId,
        Stream responseErrorStream,
        CancellationToken cancellationToken
    )
    {
        bool first2 = true;

        while (true)
        {
            CompositeBuffer? entry2;
            try
            {
                entry2 = await responseErrorStream.Shift(cancellationToken);
            }
            catch (StreamRedirectException)
            {
                await Send(
                    new ResponseErrorDonePacket() { RequestId = requestId },
                    cancellationToken
                );
                return;
            }

            if (entry2 == null)
            {
                await Send(
                    new ResponseErrorDonePacket() { RequestId = requestId },
                    cancellationToken
                );
                break;
            }
            else if (first2)
            {
                await Send(
                    new ResponseErrorBeginPacket()
                    {
                        RequestId = requestId,
                        ResponseData = entry2.ToByteArray()
                    },
                    cancellationToken
                );
                first2 = false;
            }
            else
            {
                await Send(
                    new ResponseErrorNextPacket()
                    {
                        RequestId = requestId,
                        ResponseData = entry2.ToByteArray()
                    },
                    cancellationToken
                );
            }
        }
    }

    private async Task Handle(RequestNextPacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingRequests.TryGetValue(packet.RequestId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Push(packet.RequestData, cancellationToken);
    }

    private async Task Handle(RequestDonePacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingRequests.TryRemove(packet.RequestId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Finish(cancellationToken);
    }

    private async Task Handle(RequestAbortPacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingRequests.TryRemove(packet.RequestId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Abort(null, cancellationToken);
    }

    private async Task Handle(MessageBeginPacket packet, CancellationToken cancellationToken)
    {
        Stream stream = new(cancellationToken);
        if (!Context.IncomingMessages.TryAdd(packet.MessageId, stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.DuplicateMessageId,
                cancellationToken
            );
            throw new InvalidOperationException("Duplicate request ID.");
        }

        await Context.Results.Enqueue(new HybridWebSocketResult.Message(stream), cancellationToken);

        await stream.Push(packet.MessageData, cancellationToken);
    }

    private async Task Handle(MessageNextPacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingMessages.TryGetValue(packet.MessageId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidMessageId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Push(packet.MessageData, cancellationToken);
    }

    private async Task Handle(MessageDonePacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingMessages.TryRemove(packet.MessageId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidMessageId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid message ID.");
        }

        await stream.Finish(cancellationToken);
    }

    private async Task Handle(MessageAbortPacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingMessages.TryRemove(packet.MessageId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidMessageId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid message ID.");
        }

        await stream.Abort(null, cancellationToken);
    }

    private async Task Handle(IResponseFeedPacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingResponses.TryGetValue(packet.RequestId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Push(packet.ResponseData, cancellationToken);
    }

    private async Task Handle(ResponseDonePacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingResponses.TryRemove(packet.RequestId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Finish(cancellationToken);
    }

    private async Task Handle(ResponseErrorBeginPacket packet, CancellationToken cancellationToken)
    {
        Stream errorStream = new(cancellationToken);

        if (
            !Context.IncomingResponses.TryRemove(packet.RequestId, out Stream? stream)
            || !Context.IncomingResponseErrors.TryAdd(packet.RequestId, errorStream)
        )
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Redirect(Stream.RedirectMode.Error, errorStream, cancellationToken);
        await errorStream.Push(packet.ResponseData, cancellationToken);
    }

    private async Task Handle(ResponseErrorNextPacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingResponseErrors.TryGetValue(packet.RequestId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Push(packet.ResponseData, cancellationToken);
    }

    private async Task Handle(ResponseErrorDonePacket packet, CancellationToken cancellationToken)
    {
        if (!Context.IncomingResponseErrors.TryRemove(packet.RequestId, out Stream? stream))
        {
            await SendShutdownAbrupt(
                ShutdownChannelAbruptReason.InvalidRequestId,
                cancellationToken
            );
            throw new InvalidOperationException("Invalid request ID.");
        }

        await stream.Finish(cancellationToken);
    }

    private async Task Handle(PingPacket packet, CancellationToken cancellationToken)
    {
        await Send(new PongPacket() { PingId = packet.PingId }, cancellationToken);
    }

    private async Task Handle(PongPacket packet, CancellationToken cancellationToken)
    {
        if (
            !Context.IncomingPongs.TryRemove(
                packet.PingId,
                out TaskCompletionSource? taskCompletionSource
            )
        )
        {
            await SendShutdownAbrupt(ShutdownChannelAbruptReason.InvalidPingId, cancellationToken);
            throw new InvalidOperationException("Invalid ping ID.");
        }

        taskCompletionSource.TrySetResult();
    }
}
