namespace RizzziGit.Commons.Net.HybridWebSocket2;

using Memory;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

public enum HybridWebSocketPacketType : byte
{
    InitRequest,
    InitResponse,

    RequestBegin,
    RequestNext,
    RequestDone,
    RequestAbort,

    MessageBegin,
    MessageNext,
    MessageDone,
    MessageAbort,

    ResponseBegin,
    ResponseNext,
    ResponseDone,

    ResponseErrorBegin,
    ResponseErrorNext,
    ResponseErrorDone,

    Ping,
    Pong,

    Shutdown,
    ShutdownAbrupt,
    ShutdownComplete
}

public interface IHybridWebSocketPacket { }

public interface IRequestPacket : IHybridWebSocketPacket
{
    public ulong RequestId { get; }
}

public interface IMessagePacket : IHybridWebSocketPacket
{
    public ulong MessageId { get; }
}

public interface IResponsePacket : IHybridWebSocketPacket
{
    public ulong RequestId { get; }
}

public interface IResponseFeedPacket : IResponsePacket
{
    public byte[] ResponseData { get; }
}

public interface IResponseErrorPacket : IHybridWebSocketPacket
{
    public ulong RequestId { get; }
}

public class InitRequestPacket : IHybridWebSocketPacket { }

public class InitResponsePacket : IHybridWebSocketPacket { }

public class RequestBeginPacket : IRequestPacket
{
    public required ulong RequestId;
    public required byte[] RequestData;

    ulong IRequestPacket.RequestId => RequestId;
}

public class RequestNextPacket : IRequestPacket
{
    public required ulong RequestId;
    public required byte[] RequestData;

    ulong IRequestPacket.RequestId => RequestId;
}

public class RequestDonePacket : IRequestPacket
{
    public required ulong RequestId;

    ulong IRequestPacket.RequestId => RequestId;
}

public class RequestAbortPacket : IRequestPacket
{
    public required ulong RequestId;

    ulong IRequestPacket.RequestId => RequestId;
}

public class MessageBeginPacket : IMessagePacket
{
    public required ulong MessageId;
    public required byte[] MessageData;

    ulong IMessagePacket.MessageId => MessageId;
}

public class MessageNextPacket : IMessagePacket
{
    public required ulong MessageId;
    public required byte[] MessageData;

    ulong IMessagePacket.MessageId => MessageId;
}

public class MessageDonePacket : IMessagePacket
{
    public required ulong MessageId;

    ulong IMessagePacket.MessageId => MessageId;
}

public class MessageAbortPacket : IMessagePacket
{
    public required ulong MessageId;
    ulong IMessagePacket.MessageId => MessageId;
}

public class ResponseBeginPacket : IResponseFeedPacket
{
    public required ulong RequestId;
    public required byte[] ResponseData;

    ulong IResponsePacket.RequestId => RequestId;
    byte[] IResponseFeedPacket.ResponseData => ResponseData;
}

public class ResponseNextPacket : IResponseFeedPacket
{
    public required ulong RequestId;
    public required byte[] ResponseData;

    ulong IResponsePacket.RequestId => RequestId;
    byte[] IResponseFeedPacket.ResponseData => ResponseData;
}

public class ResponseDonePacket : IResponsePacket
{
    public required ulong RequestId;

    ulong IResponsePacket.RequestId => RequestId;
}

public class ResponseErrorBeginPacket : IResponseErrorPacket
{
    public required ulong RequestId;
    public required byte[] ResponseData;

    ulong IResponseErrorPacket.RequestId => RequestId;
}

public class ResponseErrorNextPacket : IResponseErrorPacket
{
    public required ulong RequestId;
    public required byte[] ResponseData;

    ulong IResponseErrorPacket.RequestId => RequestId;
}

public class ResponseErrorDonePacket : IResponseErrorPacket
{
    public required ulong RequestId;

    ulong IResponseErrorPacket.RequestId => RequestId;
}

public class PingPacket : IHybridWebSocketPacket
{
    public required ulong PingId;
}

public class PongPacket : IHybridWebSocketPacket
{
    public required ulong PingId;
}

public class ShutdownPacket : IHybridWebSocketPacket { }

public enum ShutdownChannelAbruptReason
{
    UnexpectedPacket,
    InternalError,
    InvalidRequestId,
    InvalidMessageId,
    InvalidPingId,
    DuplicateRequestId,
    DuplicateMessageId,
    DuplicatePingId,
}

public class ShutdownAbruptPacket : IHybridWebSocketPacket
{
    public required ShutdownChannelAbruptReason Reason;
}

public class ShutdownCompletePacket : IHybridWebSocketPacket { }

class PacketWrap<T>
    where T : IHybridWebSocketPacket
{
    public static PacketWrap<T> FromBson(byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        using BsonBinaryReader reader = new(stream);

        return BsonSerializer.Deserialize<PacketWrap<T>>(reader);
    }

    public readonly string Signature = "RizzziGit HybridWebSocketPacket2";
    public required T Packet;

    public byte[] ToBson()
    {
        using MemoryStream stream = new();
        using BsonBinaryWriter writer = new(stream);

        BsonSerializer.Serialize(writer, this);
        return stream.ToArray();
    }
}

public sealed partial class HybridWebSocket
{
    private IHybridWebSocketPacket Deserialize(CompositeBuffer buffer)
    {
        try
        {
            HybridWebSocketPacketType type = (HybridWebSocketPacketType)buffer[0];
            CompositeBuffer bytes = buffer.Slice(1);

            return type switch
            {
                HybridWebSocketPacketType.InitRequest
                    => PacketWrap<InitRequestPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.InitResponse
                    => PacketWrap<InitResponsePacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.RequestBegin
                    => PacketWrap<RequestBeginPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.RequestNext
                    => PacketWrap<RequestNextPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.RequestDone
                    => PacketWrap<RequestDonePacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.RequestAbort
                    => PacketWrap<RequestAbortPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.MessageBegin
                    => PacketWrap<MessageBeginPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.MessageNext
                    => PacketWrap<MessageNextPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.MessageDone
                    => PacketWrap<MessageDonePacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.MessageAbort
                    => PacketWrap<MessageAbortPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ResponseBegin
                    => PacketWrap<ResponseBeginPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ResponseNext
                    => PacketWrap<ResponseNextPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ResponseDone
                    => PacketWrap<ResponseDonePacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ResponseErrorBegin
                    => PacketWrap<ResponseErrorBeginPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ResponseErrorNext
                    => PacketWrap<ResponseErrorNextPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ResponseErrorDone
                    => PacketWrap<ResponseErrorDonePacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.Ping
                    => PacketWrap<PingPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.Pong
                    => PacketWrap<PongPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.Shutdown
                    => PacketWrap<ShutdownPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ShutdownAbrupt
                    => PacketWrap<ShutdownAbruptPacket>.FromBson(bytes.ToByteArray()).Packet,

                HybridWebSocketPacketType.ShutdownComplete
                    => PacketWrap<ShutdownCompletePacket>.FromBson(bytes.ToByteArray()).Packet,

                _ => throw new InvalidOperationException($"Unknown packet type: {type}"),
            };
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Failed to deserialize packet", exception);
        }
    }

    private CompositeBuffer Serialize(IHybridWebSocketPacket packet)
    {
        try
        {
            (HybridWebSocketPacketType type, byte[] bytes) = packet switch
            {
                InitRequestPacket toSerialize
                    => (
                        HybridWebSocketPacketType.InitRequest,
                        new PacketWrap<InitRequestPacket>() { Packet = toSerialize }.ToBson()
                    ),
                InitResponsePacket toSerialize
                    => (
                        HybridWebSocketPacketType.InitResponse,
                        new PacketWrap<InitResponsePacket>() { Packet = toSerialize }.ToBson()
                    ),
                RequestBeginPacket toSerialize
                    => (
                        HybridWebSocketPacketType.RequestBegin,
                        new PacketWrap<RequestBeginPacket>() { Packet = toSerialize }.ToBson()
                    ),
                RequestNextPacket toSerialize
                    => (
                        HybridWebSocketPacketType.RequestNext,
                        new PacketWrap<RequestNextPacket>() { Packet = toSerialize }.ToBson()
                    ),
                RequestDonePacket toSerialize
                    => (
                        HybridWebSocketPacketType.RequestDone,
                        new PacketWrap<RequestDonePacket>() { Packet = toSerialize }.ToBson()
                    ),
                RequestAbortPacket toSerialize
                    => (
                        HybridWebSocketPacketType.RequestAbort,
                        new PacketWrap<RequestAbortPacket>() { Packet = toSerialize }.ToBson()
                    ),
                MessageBeginPacket toSerialize
                    => (
                        HybridWebSocketPacketType.MessageBegin,
                        new PacketWrap<MessageBeginPacket>() { Packet = toSerialize }.ToBson()
                    ),
                MessageNextPacket toSerialize
                    => (
                        HybridWebSocketPacketType.MessageNext,
                        new PacketWrap<MessageNextPacket>() { Packet = toSerialize }.ToBson()
                    ),
                MessageDonePacket toSerialize
                    => (
                        HybridWebSocketPacketType.MessageDone,
                        new PacketWrap<MessageDonePacket>() { Packet = toSerialize }.ToBson()
                    ),
                MessageAbortPacket toSerialize
                    => (
                        HybridWebSocketPacketType.MessageAbort,
                        new PacketWrap<MessageAbortPacket>() { Packet = toSerialize }.ToBson()
                    ),
                ResponseBeginPacket toSerialize
                    => (
                        HybridWebSocketPacketType.ResponseBegin,
                        new PacketWrap<ResponseBeginPacket>() { Packet = toSerialize }.ToBson()
                    ),
                ResponseNextPacket toSerialize
                    => (
                        HybridWebSocketPacketType.ResponseNext,
                        new PacketWrap<ResponseNextPacket>() { Packet = toSerialize }.ToBson()
                    ),
                ResponseDonePacket toSerialize
                    => (
                        HybridWebSocketPacketType.ResponseDone,
                        new PacketWrap<ResponseDonePacket>() { Packet = toSerialize }.ToBson()
                    ),
                ResponseErrorBeginPacket toSerialize
                    => (
                        HybridWebSocketPacketType.ResponseErrorBegin,
                        new PacketWrap<ResponseErrorBeginPacket>() { Packet = toSerialize }.ToBson()
                    ),
                ResponseErrorDonePacket toSerialize
                    => (
                        HybridWebSocketPacketType.ResponseErrorDone,
                        new PacketWrap<ResponseErrorDonePacket>() { Packet = toSerialize }.ToBson()
                    ),
                ResponseErrorNextPacket toSerialize
                    => (
                        HybridWebSocketPacketType.ResponseErrorNext,
                        new PacketWrap<ResponseErrorNextPacket>() { Packet = toSerialize }.ToBson()
                    ),
                PingPacket toSerialize
                    => (
                        HybridWebSocketPacketType.Ping,
                        new PacketWrap<PingPacket>() { Packet = toSerialize }.ToBson()
                    ),
                PongPacket toSerialize
                    => (
                        HybridWebSocketPacketType.Pong,
                        new PacketWrap<PongPacket>() { Packet = toSerialize }.ToBson()
                    ),
                ShutdownPacket toSerialize
                    => (
                        HybridWebSocketPacketType.Shutdown,
                        new PacketWrap<ShutdownPacket>() { Packet = toSerialize }.ToBson()
                    ),
                ShutdownAbruptPacket toSerialize
                    => (
                        HybridWebSocketPacketType.ShutdownAbrupt,
                        new PacketWrap<ShutdownAbruptPacket>() { Packet = toSerialize }.ToBson()
                    ),
                ShutdownCompletePacket toSerialize
                    => (
                        HybridWebSocketPacketType.ShutdownComplete,
                        new PacketWrap<ShutdownCompletePacket>() { Packet = toSerialize }.ToBson()
                    ),

                _
                    => throw new InvalidOperationException(
                        $"Unknown packet type: {packet.GetType()}"
                    ),
            };

            return CompositeBuffer.Concat(new byte[] { (byte)type }, bytes);
        }
        catch (Exception exception)
        {
            Fatal(exception);
            throw;
        }
    }
}
