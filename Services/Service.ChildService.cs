using RizzziGit.Commons.Threading;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    private sealed partial class ServiceInstance
    {
        public required List<IService> ChildSeviceList;
        public required SemaphoreSlim ChildSeviceListSemaphore;
    }

    protected async Task StartChildServices(
        IEnumerable<IService> services,
        CancellationToken cancellationToken
    )
    {
        List<IService> startedServices = [];

        try
        {
            foreach (IService service in services)
            {
                await StartChildService(service, cancellationToken);

                startedServices.Insert(0, service);
            }
        }
        catch (Exception exception)
        {
            List<Exception> stopExceptions = [];

            foreach (IService service in startedServices)
            {
                try
                {
                    await service.Stop();
                }
                catch (Exception stopException)
                {
                    stopExceptions.Add(stopException);
                }
            }

            if (stopExceptions.Count == 0)
            {
                throw;
            }

            throw new AggregateException([exception, .. stopExceptions]);
        }
    }

    protected async Task StartChildService(IService service, CancellationToken cancellationToken)
    {
        ServiceInstance serviceInstance = InternalContext;

        Debug($"Starting {service.Name} as a child service...", "Child Services");
        await service.Start(cancellationToken);

        WatchChildService(serviceInstance, service);
    }

    private async void WatchChildService(ServiceInstance internalInstance, IService service)
    {
        try
        {
            internalInstance.ChildSeviceListSemaphore.WithSemaphore(
                () => internalInstance.ChildSeviceList.Add(service)
            );

            await RunInconsequential(service.Watch, CancellationToken.None);
        }
        catch { }
        finally
        {
            internalInstance.ChildSeviceListSemaphore.WithSemaphore(
                () => internalInstance.ChildSeviceList.Remove(service)
            );
        }
    }
}
