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

  private static readonly Mutex CheckersMutex = new();
  private static readonly List<WeakReference<Action>> Checkers = [];
  private static Task? CheckThread;

  private static Task RunCheck()
  {
    Task task = new(async () =>
    {
      while (Checkers.Count != 0)
      {
        await GCObj.Wait();

        lock (Checkers)
        {
          for (int index = 0; index < Checkers.Count; index++)
          {
            WeakReference<Action> reference = Checkers.ElementAt(index);

            if (reference.TryGetTarget(out var action))
            {
              action();
              continue;
            }

            Checkers.RemoveAt(index++);
          }
        }
      }
    }, TaskCreationOptions.LongRunning);

    task.Start();
    return task;
  }

  private static void RunCheckThread()
  {
    CheckersMutex.WaitOne();
    if ((CheckThread?.IsCompleted != null) && (!CheckThread.IsCompleted))
    {
      return;
    }

    CheckThread = RunCheck();
    CheckersMutex.ReleaseMutex();
  }

  public static void Unregister(Action action)
  {
    lock (Checkers)
    {
      for (int index = 0; index < Checkers.Count; index++)
      {
        WeakReference<Action> reference = Checkers.ElementAt(index);

        if (reference.TryGetTarget(out var lookupAction))
        {
          if (lookupAction == action)
          {
            Checkers.RemoveAt(index++);
          }

          continue;
        }

        Checkers.RemoveAt(index++);
      }
    }
  }

  public static void Register(Action action)
  {
    lock (Checkers)
    {
      Checkers.Add(new(action));
    }
    RunCheckThread();
  }
}
