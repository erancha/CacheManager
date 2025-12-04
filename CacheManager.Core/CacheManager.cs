namespace CacheManager.Core;

using System;
using System.Collections.Generic;
using System.Linq;

public interface ICacheManager<TKey, TValue>
    where TKey : notnull
{
    void Put(TKey key, TValue value);

    bool TryGet(TKey key, out TValue? value);

    bool Remove(TKey key);

    IReadOnlyDictionary<TKey, TValue> Snapshot();
}

// Cache manager with optional capacity limit and LFU+LRU-based eviction policy.
public class CacheManager<TKey, TValue> : ICacheManager<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items = new(); // Holds the actual key/value items in the cache.
    private readonly EvictionManager<TKey>? _evictionManager; // Coordinates eviction when a maximum capacity is configured.

    public CacheManager(int? maxCapacity = null)
    {
        if (maxCapacity.HasValue && maxCapacity.Value <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(maxCapacity));
        }

        if (maxCapacity.HasValue)
        {
            _evictionManager = new EvictionManager<TKey>(maxCapacity.Value);
        }
    }

    public void Put(TKey key, TValue value)
    {
        bool exists = _items.ContainsKey(key);
        _items[key] = value;

        if (_evictionManager is not null)
        {
            // Update eviction metadata on access and evict LFU+LRU entry when capacity is exceeded.
            if (exists)
            {
                _evictionManager.OnAccess(key);
            }
            else
            {
                _evictionManager.OnAddNew(key);

                if (_evictionManager.TryGetEvictionCandidate(_items.Count, out var evictKey))
                {
                    if (!EqualityComparer<TKey>.Default.Equals(evictKey, key))
                    {
                        _items.Remove(evictKey);
                    }
                }
            }
        }
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        if (_items.TryGetValue(key, out var existing))
        {
            value = existing;

            _evictionManager?.OnAccess(key);
            return true;
        }

        value = default;
        return false;
    }

    public bool Remove(TKey key)
    {
        bool removed = _items.Remove(key);

        if (removed)
        {
            _evictionManager?.OnRemove(key);
        }

        return removed;
    }

    public IReadOnlyDictionary<TKey, TValue> Snapshot()
    {
        return new Dictionary<TKey, TValue>(_items);
    }

    public int Count => _items.Count;

    public int EvictionTrackedCount => _evictionManager?.TrackedCount ?? 0;
}

// Maintains LFU+LRU ordering and chooses which key to evict when capacity is hit.
internal sealed class EvictionManager<TKey>
    where TKey : notnull
{
    private readonly int _capacity; // Maximum number of items allowed in the associated cache.

/*
    =================================================================================================================
    _capacity = 3;
    _listsByCount -> null

    put('k1','v1') ==>

        1 -> {'k1'}

    put('k2','v1') ==>

        1 -> {'k2'} -> {'k1'} -> null

    put('k1','v2') ==>

        1 -> {'k2'} -> null
        2 -> {'k1'} -> null

    put('k2','v2') ==>

        2 -> {'k2'} -> {'k1'} -> null

    put('k3','v1') ==>

        1 -> {'k3'} -> null
        2 -> {'k2'} -> {'k1'} -> null

    put('k4','v1') ==> (4th key, _capacity == 3!)
        
        delete {'k3'}, being the key with lower frequency (1), add {'k4'} to the list of frequency 1:
        1 -> {'k4'} -> null
        2 -> {'k2'} -> {'k1'} -> null

    =================================================================================================================
    _capacity = 4;
    _listsByCount -> null

    put('k1','v1'),
    put('k2','v1'),
    put('k1','v2'),
    put('k2','v2'),
    put('k3','v1') ==> (same as when _capacity == 3)

        1 -> {'k3'} -> null
        2 -> {'k2'} -> {'k1'} -> null

    put('k4','v1') ==> 
        
        1 -> {'k4'} -> {'k3'} -> null
        2 -> {'k2'} -> {'k1'} -> null

    put('k5','v1') ==> (5th key, _capacity == 4!)
        
        delete {'k3'}, being the key with lower frequency (1) and recency, add {'k5'} to the list of frequency 1:
        1 -> {'k5'} -> {'k4'} -> null
        2 -> {'k2'} -> {'k1'} -> null
    =================================================================================================================

    Summary:
        With only the _listByFrequency, OnAccess(key) would require scanning buckets and their lists to find the key, making it O(n) in the worst case.
        With the _entryByKey, OnAccess(key) can move the key between lists in O(1).
*/

    private readonly SortedDictionary<int, LinkedList<TKey>> _listByFrequency = new(); // Maps usage count to keys ordered by recency (head = most recent, tail = least recent).
    private readonly Dictionary<TKey, KeyFrequencyEntry> _entryByKey = new(); // Per-key index into _listByFrequency.

    private sealed class KeyFrequencyEntry
    {
        public int Count;
        public LinkedListNode<TKey> Node;

        public KeyFrequencyEntry(int count, LinkedListNode<TKey> node)
        {
            Count = count;
            Node = node;
        }
    }

    public EvictionManager(int capacity)
    {
        _capacity = capacity;
    }

    public int TrackedCount => _entryByKey.Count;

    // Registers a new key with an initial usage count.
    public void OnAddNew(TKey key)
    {
        const int initialCount = 1;

        if (!_listByFrequency.TryGetValue(initialCount, out var list))
        {
            list = new LinkedList<TKey>();
            _listByFrequency[initialCount] = list;
        }

        // Example mapping to the comment above:
        //   put('k1','v1') -> list for frequency 1 becomes: {'k1'}
        //   put('k2','v1') -> AddFirst inserts 'k2' before 'k1': {'k2'} -> {'k1'} -> null
        // See LinkedList<T>.AddFirst docs: https://learn.microsoft.com/dotnet/api/system.collections.generic.linkedlist-1.addfirst
        var node = list.AddFirst(key);
        _entryByKey[key] = new KeyFrequencyEntry(initialCount, node);
    }

    // Increments the usage count and updates recency for an accessed key.
    public void OnAccess(TKey key)
    {
        if (!_entryByKey.TryGetValue(key, out var entry))
        {
            Console.WriteLine($"[WARNING] EvictionManager.OnAccess: key '{key}' not found in _entryByKey while attempting to access. This indicates an inconsistent cache state.");
            return;
        }

        int oldCount = entry.Count;
        if (_listByFrequency.TryGetValue(oldCount, out var oldList))
        {
            oldList.Remove(entry.Node); // O(1): uses the stored LinkedListNode (entry.Node) pointers, no scan over oldList
            if (oldList.Count == 0)
            {
                _listByFrequency.Remove(oldCount);
            }
        }

        int newCount = oldCount + 1;
        if (!_listByFrequency.TryGetValue(newCount, out var newList))
        {
            newList = new LinkedList<TKey>();
            _listByFrequency[newCount] = newList;
        }

        var newNode = newList.AddFirst(key); // Same explanation as in list.AddFirst(key) at the end of OnAddNew, above.
        entry.Count = newCount;
        entry.Node = newNode;
    }

    public void OnRemove(TKey key)
    {
        if (!_entryByKey.TryGetValue(key, out var entry))
        {
            Console.WriteLine($"[WARNING] EvictionManager.OnRemove: key '{key}' not found in _entryByKey while attempting to remove. This indicates an inconsistent cache state.");
            return;
        }

        if (_listByFrequency.TryGetValue(entry.Count, out var list))
        {
            list.Remove(entry.Node);
            if (list.Count == 0)
            {
                _listByFrequency.Remove(entry.Count);
            }
        }

        _entryByKey.Remove(key);
    }

    // Returns the least-frequently, least-recently used key to evict when capacity is exceeded.
    public bool TryGetEvictionCandidate(int currentItemCount, out TKey key)
    {
        key = default!;

        if (currentItemCount <= _capacity || _entryByKey.Count == 0)
        {
            return false;
        }

        var first = _listByFrequency.First();
        var list = first.Value;
        var node = list.Last!;
        key = node.Value;

        list.RemoveLast();
        if (list.Count == 0)
        {
            _listByFrequency.Remove(first.Key);
        }

        _entryByKey.Remove(key);
        return true;
    }
}
