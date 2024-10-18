using System.Runtime.CompilerServices;

namespace RizzziGit.Commons.Services;

public abstract partial class Service2<C>
{
    private sealed class ServiceInstance
    {
        public required Task Task;
        public required CancellationTokenSource CancellationTokenSource;
        public required Service2State State;
        public required StrongBox<C>? Context;
    }
}
