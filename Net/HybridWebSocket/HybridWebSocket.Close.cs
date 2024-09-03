namespace RizzziGit.Commons.Net;

public partial class HybridWebSocket
{
  public async Task Close(CancellationToken cancellationToken = default)
  {
    await SendClose(false);

    try { await (Context?.task ?? Task.CompletedTask).WaitAsync(cancellationToken); } catch {}
  }
}
