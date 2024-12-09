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
}
