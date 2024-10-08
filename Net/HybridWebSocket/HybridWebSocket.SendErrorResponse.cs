namespace RizzziGit.Commons.Net.HybridWebSocket;

using Memory;

public partial class HybridWebSocket
{
    private Task SendErrorResponse(uint id, string name, string message)
    {
        CompositeBuffer nameBuffer = CompositeBuffer.From(name);

        return SendData(
            CompositeBuffer.Concat(
                CompositeBuffer.From(DATA_ERROR_RESPONSE),
                CompositeBuffer.From(id),
                CompositeBuffer.From(nameBuffer.Length),
                CompositeBuffer.From(name),
                CompositeBuffer.From(message)
            )
        );
    }
}
