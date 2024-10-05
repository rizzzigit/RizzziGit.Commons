namespace RizzziGit.Commons.Extensions;

public static class ArrayExtensions
{
    public static (T[] Left, T[] Right) Split<T>(this T[] array, int index) =>
        (array[..index], array[index..]);
}
