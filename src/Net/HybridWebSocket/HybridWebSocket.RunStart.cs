using System.Net.WebSockets;

namespace RizzziGit.Commons.Net;

using Memory;

public partial class HybridWebSocket
{
  private List<Task> PendingDataHandlingTasks = [];

  private async Task RunStart(CancellationToken cancellationToken)
  {
    await OnStart(cancellationToken);

    while (
      ((WebSocket.State == WebSocketState.Open) || (WebSocket.State == WebSocketState.CloseSent)) &&
      ((StateInt == STATE_OPEN) || (StateInt == STATE_LOCAL_CLOSING))
    )
    {
      CompositeBuffer buffer = CompositeBuffer.Empty();
      WebSocketReceiveResult result;
      {
        byte[] bytes = new byte[1024 * 256];
        result = await WebSocket.ReceiveAsync(bytes, cancellationToken);
        buffer.Append(bytes[..result.Count]);
      }

      if (buffer.Length > Config.MaxWebSocketPerMessageSize)
      {
        try
        {
          throw new InvalidDataException("Invalid message size.");
        }
        catch (Exception exception)
        {
          PendingDataHandlingTasks.Add(Task.FromException(exception));
        }

        break;
      }

      if (result.EndOfMessage)
      {
        WatchDataHandler(HandleData(buffer.Splice(0, buffer.Length), cancellationToken));
      }
    }

    await Task.WhenAll([.. PendingDataHandlingTasks]);
  }

  private async void WatchDataHandler(Task task)
  {
    lock (PendingDataHandlingTasks)
    {
      PendingDataHandlingTasks.Add(task);
    }

    try
    {
      await task;
    }
    catch
    {
    }
    finally
    {
      lock (PendingDataHandlingTasks)
      {
        PendingDataHandlingTasks.Remove(task);
      }
    }
  }
}
