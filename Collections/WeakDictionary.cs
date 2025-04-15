using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Collections;

using System.Collections.Generic;
using GarbageCollection;
using Interfaces;

public class WeakDictionary<K, V> : IGenericDictionary<K, V>
    where K : notnull
    where V : class
{
    private sealed record Entry(int Index, K Key, V Value, Action Delete, Action<V> Update);

    private readonly List<K> InternalKeys = [];
    private readonly List<WeakReference<V>> InternalValues = [];

    private IEnumerable<Entry> Iterate()
    {
        for (int index = 0; index < InternalKeys.Count; index++)
        {
            K key = InternalKeys[index];
            WeakReference<V> valueReference = InternalValues[index];

            if (!valueReference.TryGetTarget(out V? value))
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
                        InternalValues.RemoveAt(index);

                        index--;
                        deleted = true;
                    }
                },
                (value) => InternalValues[index] = new(value)
            );
        }
    }

    public V this[K key]
    {
        get =>
            Iterate()
                .Where((entry) => EqualityComparer<K>.Default.Equals(entry.Key, key))
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
            if (EqualityComparer<K>.Default.Equals(entry.Key, key))
            {
                entry.Update(value);
                return;
            }
        }

        InternalKeys.Add(key);
        InternalValues.Add(new(value));
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
                EqualityComparer<K>.Default.Equals(entry.Key, item.Key)
                && entry.Value == item.Value
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
            if (EqualityComparer<K>.Default.Equals(entry.Key, key))
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
            if (EqualityComparer<K>.Default.Equals(entry.Key, key))
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
                EqualityComparer<K>.Default.Equals(entry.Key, item.Key)
                && entry.Value == item.Value
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
            if (EqualityComparer<K>.Default.Equals(entry.Key, key))
            {
                return false;
            }
        }

        InternalKeys.Add(key);
        InternalValues.Add(new(value));
        return true;
    }

    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
    {
        foreach (Entry entry in Iterate())
        {
            if (EqualityComparer<K>.Default.Equals(entry.Key, key))
            {
                value = entry.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() =>
        Iterate().Select((entry) => new KeyValuePair<K, V>(entry.Key, entry.Value)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
