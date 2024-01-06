namespace RizzziGit.Framework;

public static class IEnumerableExtensions
{
  public static T[] ToSorted<T>(this T[] array)
  {
    T[] newArray = [.. array];

    Array.Sort(array);
    return newArray;
  }

  public static List<T> ToSorted<T>(this List<T> list)
  {
    List<T> newList = [.. list];

    newList.Sort();
    return newList;
  }

  public static List<T> ToSorted<T>(this List<T> list, Comparison<T> comparison)
  {
    List<T> newList = [.. list];

    newList.Sort(comparison);
    return newList;
  }

  public static List<T> ToSorted<T>(this List<T> list, IComparer<T> comparer)
  {
    List<T> newList = [.. list];

    newList.Sort(comparer);
    return newList;
  }

  public static List<T> ToSorted<T>(this List<T> list, int index, int count, IComparer<T> comparer)
  {
    List<T> newList = [.. list];

    newList.Sort(index, count, comparer);
    return newList;
  }
}
