using Microsoft.Extensions.Logging;

namespace xCacheLibrary;

/// <summary>
/// Represents a thread-safe dictionary with caching capabilities.
/// </summary>
public interface IConcurrentDictionary<TKey, TValue>
{
    /// <summary>
    /// Gets the value associated with the specified key or adds a new value if the key does not exist.
    /// </summary>
    TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, ILogger logger);
    
    /// <summary>
    /// Adds a new key-value pair or updates an existing entry in the dictionary.
    /// </summary>
    void AddOrUpdate(TKey key, TValue value, ILogger logger);
    
    /// <summary>
    /// Attempts to remove a key-value pair from the dictionary.
    /// </summary>
    bool TryRemove(TKey key, ILogger logger);
    
    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    bool TryGetValue(TKey key, out TValue value, ILogger logger);
}

/// <summary>
/// Represents a concurrent cache that extends a dictionary with thread-safe caching capabilities.
/// </summary>
public interface IConcurrentXCache<Tkey, TValue> : IConcurrentDictionary<Tkey, TValue> { }