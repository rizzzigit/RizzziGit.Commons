namespace RizzziGit.Commons.Services;

public partial class Service<C>
{
    public async Task UseStart(
        Func<Service<C>, CancellationToken, Task> handle,
        CancellationToken cancellationToken
    )
    {
        await Start(cancellationToken);

        await Task.WhenAny(Watch(cancellationToken), handle(this, cancellationToken));
    }
}
