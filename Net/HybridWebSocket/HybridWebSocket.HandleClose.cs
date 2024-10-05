namespace RizzziGit.Commons.Net.HybridWebSocket;

public partial class HybridWebSocket
{
    private void HandleClose(bool isAbrupt, CancellationToken cancellationToken)
    {
        StateInt = StateInt == STATE_LOCAL_CLOSING ? STATE_CLOSED : STATE_REMOTE_CLOSING;

        if (isAbrupt)
        {
            StateInt = STATE_ABORTED;
            throw new Exception("Abrupt Closing.");
        }
    }
}
