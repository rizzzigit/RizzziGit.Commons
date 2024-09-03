using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace RizzziGit.Commons.Collections;

using Interfaces;
using GarbageCollection;

public class WeakDictionary<K, V> : IGenericDictionary<K, V>
  where K : notnull
  where V : class
{
  public WeakDictionary()
  {
    Dictionary = [];

    GarbageCollectionEventListener.Register(CheckAllItems);
  }

  ~WeakDictionary()
  {
    GarbageCollectionEventListener.Unregister(CheckAllItems);
  }

  private readonly Dictionary<K, WeakReference<V>> Dictionary;

  public int Count
  {
    get
    {
      lock (this)
      {
        return Dictionary.Count;
      }
    }
  }

  public ICollection<K> Keys => throw new NotImplementedException();
  public ICollection<V> Values => throw new NotImplementedException();

  public bool IsReadOnly => false;

  public V this[K key]
  {
    get => TryGetValue(key, out V? value) ? value : throw new KeyNotFoundException();
    set => AddOrUpdate(key, value);
  }

  public event EventHandler<K>? Finalized;

  private void CheckAllItems()
  {
    lock (this)
    {
      for (int index = 0; index < Dictionary.Count; index++)
      {
        var (key, value) = Dictionary.ElementAt(index);
        if (!value.TryGetTarget(out V? _) && Dictionary.Remove(key))
        {
          Finalized?.Invoke(this, key);

          index--;
        }
      }
    }
  }

  public void Add(K key, V value)
  {
    if (!TryAdd(key, value))
    {
      throw new ArgumentException("Key already exists.");
    }
  }

  public bool TryAdd(K key, V value)
  {
    lock (this)
    {
      if (Dictionary.TryGetValue(key, out WeakReference<V>? weakReference))
      {
        if (weakReference.TryGetTarget(out V? _))
        {
          return false;
        }
        else
        {
          weakReference.SetTarget(value);
          return true;
        }
      }
      else
      {
        Dictionary.Add(key, new(value));
        return true;
      }
    }
  }

  public void AddOrUpdate(K key, V value)
  {
    lock (this)
    {
      if (Dictionary.TryGetValue(key, out WeakReference<V>? weakReference))
      {
        weakReference.SetTarget(value);
      }
      else
      {
        Dictionary.Add(key, new(value));
      }
    }
  }

  public void Clear()
  {
    lock (this)
    {
      Dictionary.Clear();
    }
  }

  public bool Remove(K key)
  {
    lock (this)
    {
      return Dictionary.Remove(key);
    }
  }

  public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
  {
    lock (this)
    {
      if (Dictionary.TryGetValue(key, out WeakReference<V>? weakReference) && weakReference.TryGetTarget(out V? target))
      {
        value = target;
        return true;
      }

      value = null;
      return false;
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
  {
    lock (this)
    {
      foreach (var (key, value) in Dictionary)
      {
        if (value.TryGetTarget(out V? target))
        {
          yield return new(key, target);
        }
      }
    }
  }

  public bool ContainsKey(K key)
  {
    lock (this)
    {
      return Dictionary.TryGetValue(key, out WeakReference<V>? weakReference) && weakReference.TryGetTarget(out V? target);
    }
  }

  public void Add(KeyValuePair<K, V> item) => Add(item.Key, item.Value);
  public bool Contains(KeyValuePair<K, V> item) => TryGetValue(item.Key, out V? value) && value == item.Value;
  public bool Remove(KeyValuePair<K, V> item) => Remove(item.Key);
  public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
  {
    foreach (KeyValuePair<K, V> entry in array)
    {
      Add(entry);
    }
  }
}
