using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Collections;

using Interfaces;

public class WeakKeyDictionary<K, V> : IGenericDictionary<K, V>
    where K : class
{
    private sealed record Entry(int Index, K Key, V Value, Action Delete, Action<V> Update);

    private readonly List<WeakReference<K>> InternalKeys = [];
    private readonly List<V> InternalValues = [];

    private IEnumerable<Entry> Iterate()
    {
        for (int index = 0; index < InternalKeys.Count; index++)
        {
            WeakReference<K> keyReference = InternalKeys[index];
            V value = InternalValues[index];

            if (!keyReference.TryGetTarget(out K? key))
            {
                InternalKeys.RemoveAt(index);
                InternalValues.RemoveAt(index);

                index--;
                continue;
            }

            bool deleted = false;

            yield return new(
                index,
                key,
                value,
                () =>
                {
                    if (!deleted)
                    {
                        InternalKeys.RemoveAt(index);
                        InternalKeys.RemoveAt(index);

                        index--;
                        deleted = true;
                    }
                },
                (value) => InternalValues[index] = value
            );
        }
    }

    public V this[K key]
    {
        get =>
            Iterate()
                .Where((entry) => entry.Key == key)
                .Select((entry) => entry.Value)
                .FirstOrDefault() ?? throw new KeyNotFoundException();
        set => AddOrUpdate(key, value);
    }

    public ICollection<K> Keys => throw new NotImplementedException();
    public ICollection<V> Values => throw new NotImplementedException();

    public int Count => Iterate().Count();

    public bool IsReadOnly => false;

    public void Add(K key, V value)
    {
        if (!TryAdd(key, value))
        {
            throw new ArgumentException($"Duplicate key {key}.", nameof(key));
        }
    }

    public void Add(KeyValuePair<K, V> item) => Add(item.Key, item.Value);

    public void AddOrUpdate(K key, V value)
    {
        foreach (Entry entry in Iterate())
        {
            if (entry.Key == key)
            {
                entry.Update(value);
                return;
            }
        }

        InternalKeys.Add(new(key));
        InternalValues.Add(value);
    }

    public void Clear()
    {
        InternalKeys.Clear();
        InternalValues.Clear();
    }

    public bool Contains(KeyValuePair<K, V> item)
    {
        foreach (Entry entry in Iterate())
        {
            if (
                entry.Key == item.Key
                && EqualityComparer<V>.Default.Equals(item.Value, entry.Value)
            )
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsKey(K key)
    {
        foreach (Entry entry in Iterate())
        {
            if (entry.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
        foreach (Entry entry in Iterate())
        {
            if (arrayIndex < array.Length)
            {
                array[arrayIndex++] = new(entry.Key, entry.Value);
            }
        }
    }

    public bool Remove(K key)
    {
        foreach (Entry entry in Iterate())
        {
            if (entry.Key == key)
            {
                entry.Delete();

                return true;
            }
        }

        return false;
    }

    public bool Remove(KeyValuePair<K, V> item)
    {
        foreach (Entry entry in Iterate())
        {
            if (
                entry.Key == item.Key
                && EqualityComparer<V>.Default.Equals(item.Value, entry.Value)
            )
            {
                entry.Delete();

                return true;
            }
        }

        return false;
    }

    public bool TryAdd(K key, V value)
    {
        foreach (Entry entry in Iterate())
        {
            if (entry.Key == key)
            {
                return false;
            }
        }

        InternalKeys.Add(new(key));
        InternalValues.Add(value);
        return true;
    }

    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
    {
        foreach (Entry entry in Iterate())
        {
            if (entry.Key != key)
            {
                continue;
            }
        }

        value = default;
        return false;
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() =>
        Iterate().Select((entry) => new KeyValuePair<K, V>(entry.Key, entry.Value)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
