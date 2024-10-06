using System.Runtime.CompilerServices;

namespace RizzziGit.Commons.Net.HybridWebSocket2;

using Memory;

public sealed partial class HybridWebSocket
{
    public (Stream requestStream, Stream responseStream) Request()
    {
        Stream requestStream = new(CancellationToken.None);
        Stream responseStream = new(CancellationToken.None);

        ulong requestId = ++Context.NextRequestId;
        if (!Context.IncomingResponses.TryAdd(requestId, responseStream))
        {
            throw new InvalidOperationException("Duplicate request ID.");
        }

        async void send()
        {
            bool first = true;

            while (true)
            {
                CompositeBuffer? entry;

                try
                {
                    entry = await requestStream.Shift(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    await Send(
                        new RequestAbortPacket() { RequestId = requestId },
                        CancellationToken.None
                    );

                    await requestStream.Abort(exception);

                    return;
                }

                if (entry == null)
                {
                    await Send(
                        new RequestDonePacket() { RequestId = requestId },
                        CancellationToken.None
                    );
                    break;
                }
                else if (first)
                {
                    first = false;
                    await Send(
                        new RequestBeginPacket()
                        {
                            RequestId = requestId,
                            RequestData = entry.ToByteArray()
                        },
                        CancellationToken.None
                    );
                }
                else
                {
                    await Send(
                        new RequestNextPacket()
                        {
                            RequestId = requestId,
                            RequestData = entry.ToByteArray()
                        },
                        CancellationToken.None
                    );
                }
            }
        }

        send();
        return (requestStream, responseStream);
    }
}
