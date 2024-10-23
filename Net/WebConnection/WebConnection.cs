using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace RizzziGit.Commons.Net.WebConnection;

using Collections;
using Logging;
using Memory;
using Services;
using Utilities;

public sealed class WebConnectionContext
{
    public required ConcurrentDictionary<ulong, CancellationTokenSource> IncomingRequests;
    public required ConcurrentDictionary<
        ulong,
        TaskCompletionSource<WebConnectionContent>
    > IncomingResponses;

    public required WaitQueue<WebConnectionRequest?> Requests;
    public required WaitQueue<WebConnectionFeed> Feed;

    public required ulong NextRequestId;
}

public sealed class WebConnectionOptions
{
    public string Name = "Web Connection";
    public Logger? Logger = null;
}

public enum PacketType : byte
{
    Request,
    RequestCancel,
    Response,
    ResponseCancel,
    ResponseError,
    ResponseInternalError,
}

public sealed class WebConnectionRequest(TaskCompletionSource<WebConnectionContent> source)
{
    public required WebConnectionContent Request;
    public required CancellationToken CancellationToken;

    public void Reply(WebConnectionContent response) => source.SetResult(response);

    public void Except(Exception exception) =>
        source.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(exception));

    public void Cancel() => source.SetCanceled();
}

public abstract record class WebConnectionFeed
{
    private WebConnectionFeed() { }

    public sealed record class Receive : WebConnectionFeed
    {
        public required Packet Packet;
    }

    public sealed record class Send : WebConnectionFeed
    {
        public required Packet Packet;
    }

    public sealed record class Error : WebConnectionFeed
    {
        public required ExceptionDispatchInfo ExceptionDispatchInfo;
    }

    public sealed record class Done : WebConnectionFeed;
}

public sealed class WebConnectionContent
{
    public required ulong Code;
    public required CompositeBuffer Data;
}

public abstract record class Packet
{
    public required ulong Id;

    public sealed record class Request : Packet
    {
        public required ulong Code;
        public required byte[] Data;
    }

    public sealed record class RequestCancel : Packet;

    public sealed record class Response : Packet
    {
        public required ulong Code;
        public required byte[] Data;
    }

    public sealed record class ResponseCancel : Packet;

    public sealed record class ResponseError : Packet
    {
        public required byte[] Data;
        public required bool IsManuallyThrown;
    }

    public sealed record class ResponseInternalError : Packet
    {
        public required ResponseInternalErrorReason Reason;
    }

    public enum ResponseInternalErrorReason : byte
    {
        Unknown,
        InvalidId,
        SendFailure
    }

    public static Packet Deserialize(CompositeBuffer buffer)
    {
        PacketType packetType = (PacketType)buffer[0];
        CompositeBuffer packetData = buffer.Slice(1);

        using MemoryStream stream = new(packetData.ToByteArray());
        using BsonBinaryReader reader = new(stream);

        return packetType switch
        {
            PacketType.Request => BsonSerializer.Deserialize<Request>(reader),
            PacketType.RequestCancel => BsonSerializer.Deserialize<RequestCancel>(reader),
            PacketType.Response => BsonSerializer.Deserialize<Response>(reader),
            PacketType.ResponseCancel => BsonSerializer.Deserialize<ResponseCancel>(reader),
            PacketType.ResponseError => BsonSerializer.Deserialize<ResponseError>(reader),
            PacketType.ResponseInternalError
                => BsonSerializer.Deserialize<ResponseInternalError>(reader),

            _ => throw new InvalidPacketDeserializationException(packetType, packetData)
        };
    }

    public static CompositeBuffer Serialize(Packet packet)
    {
        using MemoryStream stream = new();
        using BsonBinaryWriter writer = new(stream);

        PacketType packetType;

        switch (packet)
        {
            case Request typed:
                packetType = PacketType.Request;
                BsonSerializer.Serialize(writer, typed);
                break;

            case RequestCancel typed:
                packetType = PacketType.RequestCancel;
                BsonSerializer.Serialize(writer, typed);
                break;

            case Response typed:
                packetType = PacketType.Response;
                BsonSerializer.Serialize(writer, typed);
                break;

            case ResponseCancel typed:
                packetType = PacketType.ResponseCancel;
                BsonSerializer.Serialize(writer, typed);
                break;

            case ResponseError typed:
                packetType = PacketType.ResponseError;
                BsonSerializer.Serialize(writer, typed);
                break;

            case ResponseInternalError typed:
                packetType = PacketType.ResponseInternalError;
                BsonSerializer.Serialize(writer, typed);
                break;

            default:
                throw new InvalidPacketSerializationException(packet);
        }

        return CompositeBuffer.Concat(new byte[] { (byte)packetType }, stream.ToArray());
    }

    private Packet() { }
}

public delegate Task<WebConnectionContent> WebConnectionRequestHandler(
    WebConnectionContent content,
    CancellationToken cancellationToken
);

public sealed class WebConnection(WebSocket webSocket, WebConnectionOptions options)
    : Service2<WebConnectionContext>(options.Name, options.Logger)
{
    private async Task<Packet?> Receive(CancellationToken cancellationToken)
    {
        WebSocketReceiveResult? result = null;

        try
        {
            CompositeBuffer packet = [];

            while (true)
            {
                byte[] buffer = new byte[4096];
                result = await webSocket.ReceiveAsync(buffer, cancellationToken);

                packet.Append(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return Packet.Deserialize(packet);
        }
        finally
        {
            if (result == null || result.CloseStatus != null)
            {
                await Context.Feed.Enqueue(new WebConnectionFeed.Done(), CancellationToken.None);
            }
        }
    }

    private Task Send(Packet packet) =>
        webSocket.SendAsync(
            Packet.Serialize(packet).ToByteArray(),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None
        );

    protected override Task<WebConnectionContext> OnStart(CancellationToken cancellationToken) =>
        Task.FromResult<WebConnectionContext>(
            new()
            {
                IncomingRequests = new(),
                IncomingResponses = new(),

                Feed = new(),
                Requests = new(),

                NextRequestId = 0
            }
        );

    protected override async Task OnRun(
        WebConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        List<Task> tasks = [];
        {
            tasks.Add(Task.Delay(-1, cancellationToken));
            tasks.Add(RunReceiveLoop(context.Feed, cancellationToken));
            tasks.Add(RunWorker(context, context.Feed));

            await await Task.WhenAny(tasks);
        }

        await Task.WhenAll(tasks);
    }

    private async Task RunReceiveLoop(
        WaitQueue<WebConnectionFeed> waitQueue,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Packet? packet = await Receive(cancellationToken);

            if (packet == null)
            {
                return;
            }

            await waitQueue.Enqueue(
                new WebConnectionFeed.Receive() { Packet = packet },
                CancellationToken.None
            );
        }
    }

    private async Task RunWorker(WebConnectionContext context, WaitQueue<WebConnectionFeed> feed)
    {
        await foreach (WebConnectionFeed feedEntry in feed)
        {
            if (feedEntry is WebConnectionFeed.Receive receive)
            {
                Debug($"<- {receive.Packet}", "Receive Loop");
                async void handle()
                {
                    try
                    {
                        await HandleReceivedPacket(context, feed, receive);
                    }
                    catch (Exception exception)
                    {
                        await feed.Enqueue(
                            new WebConnectionFeed.Error
                            {
                                ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception),
                            }
                        );
                    }
                }

                handle();
            }
            else if (feedEntry is WebConnectionFeed.Send send)
            {
                Debug($"-> {send.Packet}", "Receive Loop");
                await Send(send.Packet);
            }
            else if (feedEntry is WebConnectionFeed.Error error)
            {
                error.ExceptionDispatchInfo.Throw();
            }
            else if (feedEntry is WebConnectionFeed.Done)
            {
                Debug($"Done", "Receive Loop");
                await context.Requests.Enqueue(null);

                break;
            }
        }
    }

    private async Task HandleReceivedPacket(
        WebConnectionContext context,
        WaitQueue<WebConnectionFeed> waitQueue,
        WebConnectionFeed.Receive receive
    )
    {
        if (receive.Packet is Packet.Response response)
        {
            if (
                !context.IncomingResponses.TryRemove(
                    response.Id,
                    out TaskCompletionSource<WebConnectionContent>? incomingResponse
                )
            )
            {
                return;
            }

            incomingResponse.SetResult(
                new WebConnectionContent() { Code = response.Code, Data = response.Data }
            );
        }
        else if (receive.Packet is Packet.ResponseCancel responseCancel)
        {
            if (
                !context.IncomingResponses.TryRemove(
                    responseCancel.Id,
                    out TaskCompletionSource<WebConnectionContent>? incomingResponse
                )
            )
            {
                return;
            }

            incomingResponse.SetCanceled(default);
        }
        else if (receive.Packet is Packet.ResponseError responseError)
        {
            if (
                !context.IncomingResponses.TryRemove(
                    responseError.Id,
                    out TaskCompletionSource<WebConnectionContent>? incomingResponse
                )
            )
            {
                return;
            }

            incomingResponse.SetException(
                ExceptionDispatchInfo.SetCurrentStackTrace(
                    new WebConnectionResponseException(
                        responseError.Data,
                        responseError.IsManuallyThrown
                    )
                )
            );
        }
        else if (receive.Packet is Packet.ResponseInternalError internalResponseError)
        {
            if (
                !context.IncomingResponses.TryRemove(
                    internalResponseError.Id,
                    out TaskCompletionSource<WebConnectionContent>? incomingResponse
                )
            )
            {
                return;
            }

            incomingResponse.SetException(
                ExceptionDispatchInfo.SetCurrentStackTrace(
                    new WebConnectionInternalResponse(internalResponseError.Reason)
                )
            );
        }
        else if (receive.Packet is Packet.Request request)
        {
            using CancellationTokenSource requestCancellationTokenSource = new();

            if (!context.IncomingRequests.TryAdd(request.Id, requestCancellationTokenSource))
            {
                await waitQueue.Enqueue(
                    new WebConnectionFeed.Send
                    {
                        Packet = new Packet.ResponseInternalError
                        {
                            Id = request.Id,
                            Reason = Packet.ResponseInternalErrorReason.InvalidId
                        },
                    }
                );

                return;
            }

            try
            {
                WebConnectionContent content;

                try
                {
                    TaskCompletionSource<WebConnectionContent> source = new();

                    await context.Requests.Enqueue(
                        new(source)
                        {
                            Request = new WebConnectionContent()
                            {
                                Code = request.Code,
                                Data = request.Data,
                            },

                            CancellationToken = requestCancellationTokenSource.Token
                        }
                    );

                    content = await source.Task;
                }
                catch (Exception exception)
                {
                    if (
                        exception is OperationCanceledException operationCanceledException
                        && operationCanceledException.CancellationToken
                            == requestCancellationTokenSource.Token
                    )
                    {
                        await waitQueue.Enqueue(
                            new WebConnectionFeed.Send
                            {
                                Packet = new Packet.ResponseCancel { Id = request.Id }
                            }
                        );
                    }
                    else
                    {
                        CompositeBuffer errorData;
                        bool isManuallyThrown;

                        if (exception is WebConnectionResponseException webException)
                        {
                            errorData = webException.ErrorData;
                            isManuallyThrown = true;
                        }
                        else
                        {
                            errorData = [];
                            isManuallyThrown = false;
                        }

                        await waitQueue.Enqueue(
                            new WebConnectionFeed.Send
                            {
                                Packet = new Packet.ResponseError
                                {
                                    Id = request.Id,
                                    Data = errorData.ToByteArray(),
                                    IsManuallyThrown = isManuallyThrown
                                }
                            }
                        );
                    }
                    return;
                }

                await waitQueue.Enqueue(
                    new WebConnectionFeed.Send
                    {
                        Packet = new Packet.Response()
                        {
                            Id = request.Id,
                            Code = content.Code,
                            Data = content.Data.ToByteArray()
                        }
                    }
                );
            }
            finally
            {
                context.IncomingRequests.TryRemove(request.Id, out _);
            }
        }
        else if (receive.Packet is Packet.RequestCancel requestCancel)
        {
            if (
                !context.IncomingRequests.TryRemove(
                    requestCancel.Id,
                    out CancellationTokenSource? requestCancellationToken
                )
            )
            {
                return;
            }

            requestCancellationToken.Cancel();
        }
    }

    public Task<WebConnectionRequest?> ReceiveRequest(CancellationToken cancellationToken) =>
        Context.Requests.Dequeue(cancellationToken);

    public async Task<WebConnectionContent> SendRequest(
        WebConnectionContent content,
        CancellationToken cancellationToken = default
    )
    {
        while (true)
        {
            TaskCompletionSource<WebConnectionContent> source = new();
            ulong requestId = Context.NextRequestId++;

            if (!Context.IncomingResponses.TryAdd(requestId, source))
            {
                continue;
            }

            async void waitCancellation()
            {
                await cancellationToken.GetTask();

                await Context.Feed.Enqueue(
                    new WebConnectionFeed.Send
                    {
                        Packet = new Packet.RequestCancel { Id = requestId }
                    },
                    CancellationToken.None
                );
            }

            waitCancellation();

            await Context.Feed.Enqueue(
                new WebConnectionFeed.Send
                {
                    Packet = new Packet.Request
                    {
                        Id = requestId,
                        Code = content.Code,
                        Data = content.Data.ToByteArray()
                    }
                },
                CancellationToken.None
            );

            return await source.Task;
        }
    }
}

public abstract class WebConnectionException(string? message, Exception? inner = null)
    : Exception(message, inner);

public sealed class InvalidPacketDeserializationException(
    PacketType packetType,
    CompositeBuffer packetData,
    Exception? inner = null
)
    : WebConnectionException(
        $"Invalid serialization packet: 0x{Convert.ToString((byte)packetType, 16)}",
        inner
    )
{
    public readonly PacketType PacketType = packetType;
    public readonly CompositeBuffer PacketData = packetData;
}

public sealed class InvalidPacketSerializationException(Packet packet, Exception? inner = null)
    : WebConnectionException($"Invalid serialization packet: {packet}", inner)
{
    public readonly Packet Packet = packet;
}

public sealed class WebConnectionResponseException : WebConnectionException
{
    internal WebConnectionResponseException(
        CompositeBuffer errorData,
        bool isManuallyThrown,
        Exception? inner = null
    )
        : base($"Remote returned an error.", inner)
    {
        ErrorData = errorData;
        IsManuallyThrown = isManuallyThrown;
    }

    public WebConnectionResponseException(CompositeBuffer errorData, Exception? inner = null)
        : this(errorData, true, inner) { }

    public readonly CompositeBuffer ErrorData;
    public readonly bool IsManuallyThrown;
}

public sealed class WebConnectionInternalResponse(
    Packet.ResponseInternalErrorReason reason,
    Exception? inner = null
) : WebConnectionException($"Remote returned an internal error.", inner)
{
    public readonly Packet.ResponseInternalErrorReason Reason = reason;
}
