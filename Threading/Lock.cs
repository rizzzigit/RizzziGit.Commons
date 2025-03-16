namespace RizzziGit.Commons.Threading;

public static class LockExtensions
{
    public static void WithLock<O>(this O obj, Action action)
        where O : notnull
    {
        lock (obj)
        {
            action();
        }
    }

    public static void WithLock<O>(this O obj, Action<O> action)
        where O : notnull => WithLock(obj, () => action(obj));

    public static T WithLock<T, O>(this O obj, Func<T> action)
        where O : notnull
    {
        lock (obj)
        {
            return action();
        }
    }

    public static T WithLock<T, O>(this O obj, Func<O, T> action)
        where O : notnull => WithLock(obj, () => action(obj));
}
