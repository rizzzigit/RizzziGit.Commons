namespace RizzziGit.Commons.Collections.Interfaces;

internal interface IGenericDictionary<K, V> : IDictionary<K, V>
{
  public bool TryAdd(K key, V value);
  public void AddOrUpdate(K key, V value);
}
