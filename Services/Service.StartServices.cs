using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Services;

public abstract partial class Service<C>
{
    public static async Task StartServices(
        IService[] services,
        CancellationToken cancellationToken = default
    )
    {
        List<IService> startedServices = [];
        try
        {
            foreach (IService service in services)
            {
                await service.Start(cancellationToken);

                startedServices.Add(service);
            }
        }
        catch (Exception exception)
        {
            List<Exception> stopExceptions = [];

            foreach (IService service in startedServices.Reverse<IService>())
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
}
