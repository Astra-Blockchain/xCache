using Microsoft.Extensions.Logging;

namespace xCacheLibrary;

public interface IConcurrentDictionary<TKey, TValue>
{
    TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, ILogger logger);
    
    void AddOrUpdate(TKey key, TValue value, ILogger logger);
    
    bool TryRemove(TKey key, ILogger logger);
    
    bool TryGetValue(TKey key, out TValue value, ILogger logger);
}

/// <summary>
/// Represents a concurrent cache that extends a dictionary with thread-safe caching capabilities.
/// </summary>
public interface IConcurrentXCache<Tkey, TValue> : IConcurrentDictionary<Tkey, TValue> { }