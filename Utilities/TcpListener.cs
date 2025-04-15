using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace RizzziGit.Commons.Utilities;

public static class TcpListenerExtensions
{
    extension (TcpListener listener)
    {
        public async IAsyncEnumerable<TcpClient> ToAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            while (true)
            {
                yield return await listener.AcceptTcpClientAsync(cancellationToken);
            }
        }
    }
}
