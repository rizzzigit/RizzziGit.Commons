using System.Net.WebSockets;

namespace RizzziGit.Framework.Net;

using Memory;

public abstract partial class HybridWebSocket(ConnectionConfig config, WebSocket webSocket) : IDisposable
{
  private const byte DATA_MESSAGE = 0x00;
  private const byte DATA_REQUEST = 0x01;
  private const byte DATA_RESPONSE = 0x02;
  private const byte DATA_CANCEL_REQUEST = 0x03;
  private const byte DATA_CANCEL_RESPONSE = 0x04;
  private const byte DATA_ERROR_RESPONSE = 0x05;
  private const byte DATA_CLOSE = 0x06;

  public HybridWebSocket(ConnectionConfig config, Stream stream, bool isServer, string? subProtocol = null, TimeSpan? keepInterval = null) : this(config, WebSocket.CreateFromStream(stream, isServer, subProtocol, keepInterval ?? TimeSpan.Zero)) { }

  private readonly WebSocket WebSocket = webSocket;
  protected readonly ConnectionConfig Config = config;

  protected abstract Task OnStart(CancellationToken cancellationToken);
  protected abstract Task<(uint responseCode, CompositeBuffer responsePayload)> OnRequest(uint requestCode, CompositeBuffer requestPayload, CancellationToken cancellationToken);
  protected abstract Task OnMessage(CompositeBuffer message, CancellationToken cancellationToken);

  void IDisposable.Dispose() => GC.SuppressFinalize(this);
}
