namespace CacheManager.Core;

using System.Collections.Generic;

public interface ICacheManager<TKey, TValue>
    where TKey : notnull
{
    void Put(TKey key, TValue value);

    bool TryGet(TKey key, out TValue? value);

    bool Remove(TKey key);

    IReadOnlyDictionary<TKey, TValue> Snapshot();
}

public class CacheManager<TKey, TValue> : ICacheManager<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items = new();

    public void Put(TKey key, TValue value)
    {
        _items[key] = value;
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        if (_items.TryGetValue(key, out var existing))
        {
            value = existing;
            return true;
        }

        value = default;
        return false;
    }

    public bool Remove(TKey key)
    {
        return _items.Remove(key);
    }

    public IReadOnlyDictionary<TKey, TValue> Snapshot()
    {
        return new Dictionary<TKey, TValue>(_items);
    }
}
