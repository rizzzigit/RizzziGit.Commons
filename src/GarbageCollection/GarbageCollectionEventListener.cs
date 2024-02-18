namespace RizzziGit.Framework.GarbageCollection;

internal class GarbageCollectionEventListener
{
  private class GCObj
  {
    public static Task Wait() => new GCObj(new()).Source.Task;

    private GCObj(TaskCompletionSource source) => Source = source;
    private readonly TaskCompletionSource Source;
    ~GCObj() => Source.SetResult();
  }

  private static readonly Dictionary<WeakReference<Action>, CancellationTokenSource> CancellationTokenSources = [];

  private static void Register(WeakReference<Action> action)
  {
    CancellationTokenSource cancellationTokenSource = new();

    lock (CancellationTokenSources)
    {
      CancellationTokenSources.Add(action, cancellationTokenSource);
    }

    wait(action);

    return;

    void wait(WeakReference<Action> action) => GCObj.Wait().ContinueWith((_) => check(action));
    void check(WeakReference<Action> action)
    {
      Action handler;
      lock (CancellationTokenSources)
      {
        if (!action.TryGetTarget(out Action? target) || cancellationTokenSource.IsCancellationRequested)
        {
          try { cancellationTokenSource.Dispose(); } catch { }
          CancellationTokenSources.Remove(action);

          return;
        }

        handler = target;
      }

      handler();
      wait(action);
    }
  }

  public static void Register(Action action) => Register(new WeakReference<Action>(action));

  public static void Unregister(Action action)
  {
    lock (CancellationTokenSources)
    {
      foreach (var (weakReference, cancellationTokenSource) in CancellationTokenSources)
      {
        if (!weakReference.TryGetTarget(out Action? target) || target != action)
        {
          continue;
        }

        try { cancellationTokenSource.Cancel(); } catch { };
      }
    }
  }
}
