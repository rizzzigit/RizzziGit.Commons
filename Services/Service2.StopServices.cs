using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Services;

public abstract partial class Service2<C>
{
    public static async Task StopServices(params IService2[] services)
    {
        List<ExceptionDispatchInfo> stopExceptions = [];

        foreach (IService2 service in services)
        {
            try
            {
                await service.Stop();
            }
            catch (Exception exception)
            {
                stopExceptions.Add(ExceptionDispatchInfo.Capture(exception));
            }
        }

        if (stopExceptions.Count == 0)
        {
            return;
        }

        throw new AggregateException(
            [.. stopExceptions.Select((exception) => exception.SourceException)]
        );
    }
}
