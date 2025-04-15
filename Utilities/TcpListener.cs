using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace RizzziGit.Commons.Utilities;

public static class TcpListenerExtensions
{
    public static async IAsyncEnumerable<TcpClient> ToAsyncEnumerable(
        this TcpListener listener,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        while (true)
        {
            yield return await listener.AcceptTcpClientAsync(cancellationToken);
        }
    }
}
