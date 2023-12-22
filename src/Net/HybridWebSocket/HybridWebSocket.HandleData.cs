namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket
{
  public event Action<byte>? OnReceive;

  private async Task HandleData(CompositeBuffer data, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    OnReceive?.Invoke(data[0]);
    switch (data[0])
    {
      case DATA_MESSAGE:
        await OnMessage(data.Slice(1), cancellationToken);
        break;

      case DATA_REQUEST:
        await HandleRequest(data.Slice(1, 5).ToUInt32(), data.Slice(5, 9).ToUInt32(), data.Slice(9), cancellationToken);
        break;

      case DATA_CANCEL_REQUEST:
        HandleCancelRequest(data.Slice(1, 5).ToUInt32(), cancellationToken);
        break;

      case DATA_RESPONSE:
        HandleResponse(data.Slice(1, 5).ToUInt32(), data.Slice(5, 9).ToUInt32(), data.Slice(9));
        break;

      case DATA_CANCEL_RESPONSE:
        HandleCancelResponse(data.Slice(1, 5).ToUInt32());
        break;

      case DATA_ERROR_RESPONSE:
        {
          long nameLength = data.Slice(5, 13).ToInt64();

          HandleErrorResponse(data.Slice(1, 5).ToUInt32(), data.Slice(13, 13 + nameLength).ToString(), data.Slice(13 + nameLength).ToString());
          break;
        }

      case DATA_CLOSE:
        HandleClose(data.Slice(1, 2)[0] == 0x01, cancellationToken);
        break;

      default: throw new InvalidDataException($"Unknown data type: 0x{Convert.ToHexString(new byte[] { data[0] })}");
    }
  }
}
