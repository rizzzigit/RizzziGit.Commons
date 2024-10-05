namespace RizzziGit.Commons.Net.HybridWebSocket;

using Memory;

public partial class HybridWebSocket
{
    private Task SendCancelResponse(uint id) =>
        SendData(
            CompositeBuffer.Concat(
                CompositeBuffer.From(DATA_CANCEL_RESPONSE),
                CompositeBuffer.From(id)
            )
        );
}
