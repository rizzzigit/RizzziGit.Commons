namespace RizzziGit.Commons.Tasks;

public static class TaskExtensions
{
    public static async Task<T> Then<V, T>(this Task<V> task, Func<V, Task<T>> callback) =>
        await callback(await task);

    public static async Task<T> Then<V, T>(this Task<V> task, Func<V, T> callback) =>
        callback(await task);

    public static async Task Then<V>(this Task<V> task, Action<V> callback) => callback(await task);

    public static async Task Then<V>(this Task<V> task, Func<V, Task> callback) =>
        await callback(await task);

    public static async Task<T> Then<T>(this Task task, Func<Task<T>> callback)
    {
        await task;
        return await callback();
    }

    public static async Task<T> Then<T>(this Task task, Func<T> callback)
    {
        await task;
        return callback();
    }

    public static async Task Then(this Task task, Func<Task> callback)
    {
        await task;
        await callback();
    }

    public static async Task Then(this Task task, Action callback)
    {
        await task;
        callback();
    }
}
