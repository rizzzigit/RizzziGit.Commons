namespace RizzziGit.Framework.Services;

public class WatchDogException(Service service, Exception? exception) : Exception($"{service.Name} service has stopped.", exception)
{
  public readonly Service Service = service;
}
