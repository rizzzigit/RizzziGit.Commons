using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Collections;

using Interfaces;
using GarbageCollection;

public class DoublyWeakDictionary<K, V> : IGenericDictionary<K, V>
  where K : class
  where V : class
{
  public DoublyWeakDictionary()
  {
    Dictionary = [];

    GarbageCollectionEventListener.Register(CheckAllItems);
  }

  ~DoublyWeakDictionary()
  {
    GarbageCollectionEventListener.Unregister(CheckAllItems);
  }

  private readonly Dictionary<WeakReference<K>, WeakReference<V>> Dictionary;

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
    get => TryGetValue(key, out V? target) ? target : throw new KeyNotFoundException();
    set => AddOrUpdate(key, value);
  }

  public event EventHandler<V>? KeyFinalized;
  public event EventHandler<K>? ValueFinalized;

  private void CheckAllItems()
  {
    lock (this)
    {
      foreach (var (key, value) in Dictionary)
      {
        if (!key.TryGetTarget(out K? _) && Dictionary.Remove(key))
        {
          if (value.TryGetTarget(out V? valueTarget))
          {
            KeyFinalized?.Invoke(this, valueTarget);
          }
        }
        else if (!value.TryGetTarget(out V? _) && Dictionary.Remove(key))
        {
          if (key.TryGetTarget(out K? keyTarget))
          {
            ValueFinalized?.Invoke(this, keyTarget);
          }
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
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? keyTarget) && keyTarget == key)
        {
          if (lookup.Value.TryGetTarget(out V? _))
          {
            return false;
          }

          lookup.Value.SetTarget(value);
          return true;
        }
      }

      Dictionary.Add(new(key), new(value));
      return true;
    }
  }

  public void AddOrUpdate(K key, V value)
  {
    lock (this)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? keyTarget) && keyTarget == key)
        {
          lookup.Value.SetTarget(value);
          return;
        }
      }

      Dictionary.Add(new(key), new(value));
    }
  }

  public void Clear()
  {
    lock (this)
    {
      Dictionary.Clear();
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
  {
    lock (this)
    {
      foreach (var (key, value) in Dictionary)
      {
        if (key.TryGetTarget(out K? keyTarget) && value.TryGetTarget(out V? valueTarget))
        {
          yield return new(keyTarget, valueTarget);
        }
      }
    }
  }

  public bool Remove(K key)
  {
    lock (this)
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
    value = null;

    lock (this)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? keyTarget) && keyTarget == key)
        {
          if (lookup.Value.TryGetTarget(out V? valueTarget))
          {
            value = valueTarget;
            return true;
          }
        }
      }

      return false;
    }
  }

  public bool ContainsKey(K key)
  {
    lock (this)
    {
      foreach (var lookup in Dictionary)
      {
        if (lookup.Key.TryGetTarget(out K? keyTarget) && keyTarget == key)
        {
          if (lookup.Value.TryGetTarget(out V? valueTarget))
          {
            return true;
          }
        }
      }

      return false;
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
