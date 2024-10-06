using System.Runtime.CompilerServices;

namespace RizzziGit.Commons.Net.HybridWebSocket2;

using Collections;
using Memory;
using Utilities;

public sealed partial class HybridWebSocket
{
    public Stream Message()
    {
        Stream stream = new(CancellationToken);

        ulong messageId = ++Context.NextMessageId;
        async void start()
        {
            bool first = true;

            while (true)
            {
                CompositeBuffer? buffer = await stream.Shift(CancellationToken);
                if (buffer == null)
                {
                    await Send(
                        new MessageDonePacket() { MessageId = messageId },
                        CancellationToken.None
                    );
                    break;
                }
                else if (first)
                {
                    await Send(
                        new MessageBeginPacket()
                        {
                            MessageId = messageId,
                            MessageData = buffer.ToByteArray()
                        },
                        CancellationToken.None
                    );

                    first = false;
                    continue;
                }
                else
                {
                    await Send(
                        new MessageNextPacket()
                        {
                            MessageId = messageId,
                            MessageData = buffer.ToByteArray()
                        },
                        CancellationToken.None
                    );
                }
            }
        }

        start();
        return stream;
    }
}
