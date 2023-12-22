using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace RizzziGit.Framework.Collections;

using Interfaces;
using GarbageCollection;

public class WeakKeyDictionary<K, V> : IGenericDictionary<K, V>
  where K : class
{
  public WeakKeyDictionary()
  {
    Dictionary = new();

    GarbageCollectionEventListener.Register(CheckAllItems);
  }

  ~WeakKeyDictionary()
  {
    GarbageCollectionEventListener.Unregister(CheckAllItems);
  }

  private readonly Dictionary<WeakReference<K>, V> Dictionary;

  public int Count
  {
    get
    {
      lock (Dictionary)
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
    get => TryGetValue(key, out V? target) ? target : throw new KeyNotFoundException();
    set => AddOrUpdate(key, value);
  }

  public event EventHandler<V>? Finalized;

  private void CheckAllItems()
  {
    lock (Dictionary)
    {
      for (int index = 0; index < Dictionary.Count; index++)
      {
        var (key, value) = Dictionary.ElementAt(index);
        if (!key.TryGetTarget(out K? _) && Dictionary.Remove(key))
        {
          Finalized?.Invoke(this, value);

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
    lock (Dictionary)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? target) && target == key)
        {
          return false;
        }
      }

      Dictionary.Add(new(key), value);
      return true;
    }
  }

  public void AddOrUpdate(K key, V value)
  {
    lock (Dictionary)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? target) && target == key)
        {
          Dictionary.Remove(lookup.Key);
          Dictionary.Add(new(key), value);
          return;
        }
      }

      Dictionary.Add(new(key), value);
    }
  }

  public void Clear()
  {
    lock (Dictionary)
    {
      Dictionary.Clear();
    }
  }

  public bool Remove(K key)
  {
    lock (Dictionary)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? target) && target == key)
        {
          Dictionary.Remove(lookup.Key);
          return true;
        }
      }

      return false;
    }
  }

  public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
  {
    lock (Dictionary)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? target) && target == key)
        {
          value = lookup.Value;
          return true;
        }
      }

      value = default;
      return false;
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
  {
    lock (Dictionary)
    {
      foreach (var (key, value) in Dictionary)
      {
        if (key.TryGetTarget(out K? target))
        {
          yield return new(target, value);
        }
      }
    }
  }

  public bool ContainsKey(K key)
  {
    lock (Dictionary)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? target) && target == key)
        {
          return true;
        }
      }

      return false;
    }
  }

  public void Add(KeyValuePair<K, V> item) => Add(item.Key, item.Value);
  public bool Contains(KeyValuePair<K, V> item) => TryGetValue(item.Key, out V? value) && Equals(value, item.Value);
  public bool Remove(KeyValuePair<K, V> item) => Remove(item.Key);
  public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
  {
    foreach (KeyValuePair<K, V> entry in array)
    {
      Add(entry);
    }
  }
}
