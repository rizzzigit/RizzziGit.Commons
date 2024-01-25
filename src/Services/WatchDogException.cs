namespace RizzziGit.Framework.Services;

public class WatchDogException(Service service, Exception? exception) : Exception($"{service.GetType().Name} service has stopped.", exception)
{
  public readonly Service Service = service;
}
