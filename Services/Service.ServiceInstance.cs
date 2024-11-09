using System.Runtime.CompilerServices;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    private sealed class ServiceInstance
    {
        public required Task Task;
        public required CancellationTokenSource CancellationTokenSource;
        public required ServiceState State;
        public required StrongBox<C>? Context;
        public required List<Task> WaitTasksBeforeStopping;
    }
}
