namespace RizzziGit.Framework.Net;

public abstract partial class HybridWebSocket
{
  public async Task Close(CancellationToken? cancellationToken = null)
  {
    await SendClose(false);

    try { await (Context?.task ?? Task.CompletedTask).WaitAsync(cancellationToken ?? CancellationToken.None); } catch {}
  }
}
