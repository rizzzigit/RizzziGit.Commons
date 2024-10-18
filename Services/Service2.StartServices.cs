using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Services;

public abstract partial class Service2<C>
{
    public static async Task StartServices(
        IService2[] services,
        CancellationToken cancellationToken = default
    )
    {
        List<IService2> startedServices = [];
        try
        {
            foreach (IService2 service in services)
            {
                await service.Start(cancellationToken);

                startedServices.Add(service);
            }
        }
        catch (Exception exception)
        {
            List<ExceptionDispatchInfo> stopExceptions = [];

            foreach (IService2 service in services)
            {
                try
                {
                    await service.Stop();
                }
                catch (Exception stopException)
                {
                    stopExceptions.Add(ExceptionDispatchInfo.Capture(stopException));
                }
            }

            if (stopExceptions.Count == 0)
            {
                throw;
            }

            throw new AggregateException(
                [exception, .. stopExceptions.Select((exception) => exception.SourceException)]
            );
        }
    }
}
