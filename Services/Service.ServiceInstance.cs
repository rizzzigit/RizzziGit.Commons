using System.Runtime.CompilerServices;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    private sealed partial class ServiceInstance
    {
        public required Task Task;
        public required CancellationTokenSource CancellationTokenSource;
        public required ServiceState State;
        public required StrongBox<C>? Context;

    }
}
