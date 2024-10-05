namespace RizzziGit.Commons.Net.HybridWebSocket;

using Memory;

public partial class HybridWebSocket
{
    private Task SendResponse(uint id, uint responseCode, CompositeBuffer responsePayload) =>
        SendData(
            CompositeBuffer.Concat(
                CompositeBuffer.From(DATA_RESPONSE),
                CompositeBuffer.From(id),
                CompositeBuffer.From(responseCode),
                responsePayload
            )
        );
}
