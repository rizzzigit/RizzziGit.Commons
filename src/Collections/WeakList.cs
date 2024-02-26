using System.Collections;

namespace RizzziGit.Framework.Collections;

public class WeakList<T> : IList<T>
  where T : class
{
  public WeakList(params T[] values)
  {
    List = values.Select((e) => new WeakReference<T>(e)).ToList();
  }

  public WeakList()
  {
    List = [];
  }

  private readonly List<WeakReference<T>> List;

  public T this[int index]
  {
    get
    {
#nullable disable
      List[index].TryGetTarget(out T target);
      return target;
#nullable enable
    }

    set => List[index] = new(value);
  }

  public int Count => List.Count;

  public bool IsReadOnly => false;

  public void Purge()
  {
    lock (this)
    {
      for (int index = 0; index < List.Count; index++)
      {
        if (!List[index].TryGetTarget(out T? _))
        {
          List.RemoveAt(index--);
        }
      }
    }
  }

  public int IndexOf(T? item)
  {
    lock (this)
    {
      for (int index = 0; index < List.Count; index++)
      {
        if (List[index].TryGetTarget(out T? _))
        {
          return index;
        }
      }

      return -1;
    }
  }

  public void Insert(int index, T item)
  {
    lock (this)
    {
      List.Insert(index, new(item));
    }
  }

  public void RemoveAt(int index)
  {
    lock (this)
    {
      List.RemoveAt(index);
    }
  }

  public void Add(T item)
  {
    lock (this)
    {
      List.Add(new(item));
    }
  }

  public void Clear()
  {
    lock (this)
    {
      List.Clear();
    }
  }

  public bool Contains(T item)
  {
    lock (this)
    {
      for (int index = 0; index < List.Count; index++)
      {
        if (List[index].TryGetTarget(out T? _))
        {
          return true;
        }
      }

      return false;
    }
  }

  public void CopyTo(T[] array, int arrayIndex)
  {
    lock (this)
    {
      if ((arrayIndex > array.Length) || (arrayIndex < 0) || (array.Length - arrayIndex) < Count)
      {
        throw new ArgumentOutOfRangeException(nameof(arrayIndex));
      }

      for (int index = arrayIndex; index < array.Length; index++)
      {
#nullable disable
        array[index] = List[index].TryGetTarget(out T target) ? target : null;
#nullable enable
      }
    }
  }

  public bool Remove(T item)
  {
    lock (this)
    {
      for (int index = 0; index < List.Count; index++)
      {
        if (List[index].TryGetTarget(out T? target) && (target == item))
        {
          List.RemoveAt(index--);
          return true;
        }
      }

      return false;
    }
  }

  public IEnumerator<T> GetEnumerator()
  {
    lock (this)
    {
      for (int index = 0; index < List.Count; index++)
      {
        if (List[index].TryGetTarget(out T? target))
        {
          yield return target;
        }
      }
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
