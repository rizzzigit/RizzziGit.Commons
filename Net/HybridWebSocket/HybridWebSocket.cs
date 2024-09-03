using System.Net.WebSockets;

namespace RizzziGit.Commons.Net;

using Memory;

public partial class HybridWebSocket(HybridWebSocketConfig config, WebSocket webSocket) : IDisposable
{
  private const byte DATA_MESSAGE = 0x00;
  private const byte DATA_REQUEST = 0x01;
  private const byte DATA_RESPONSE = 0x02;
  private const byte DATA_CANCEL_REQUEST = 0x03;
  private const byte DATA_CANCEL_RESPONSE = 0x04;
  private const byte DATA_ERROR_RESPONSE = 0x05;
  private const byte DATA_CLOSE = 0x06;

  public sealed record Payload(uint Code, CompositeBuffer Buffer);

  public HybridWebSocket(HybridWebSocketConfig config, Stream stream, bool isServer, string? subProtocol = null, TimeSpan? keepInterval = null) : this(config, WebSocket.CreateFromStream(stream, isServer, subProtocol, keepInterval ?? TimeSpan.Zero)) { }

  private readonly WebSocket WebSocket = webSocket;
  protected readonly HybridWebSocketConfig Config = config;

  protected virtual Task OnStart(CancellationToken cancellationToken) { return Task.CompletedTask; }
  protected virtual Task<Payload> OnRequest(Payload payload, CancellationToken cancellationToken) { throw new NotImplementedException(); }
  protected virtual Task OnMessage(CompositeBuffer message, CancellationToken cancellationToken) { return Task.CompletedTask; }

  void IDisposable.Dispose() => GC.SuppressFinalize(this);
}
