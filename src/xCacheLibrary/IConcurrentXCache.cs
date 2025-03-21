// --------------------------------------------------------
// Copyright (c) Astra. All rights reserved.
// 
// Author:  Giovanny Hernandez
// Created: March 19, 2025
// --------------------------------------------------------

namespace xCacheLibrary;

/// <summary>
/// Represents a thread-safe dictionary with caching capabilities.
/// </summary>
public interface IConcurrentDictionary<TKey, TValue>
{
    /// <summary>
    /// Gets the value associated with the specified key or adds a new value if the key does not exist.
    /// </summary>
    TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory);

    /// <summary>
    /// Gets the value associated with the specified key or adds a new value if the key does not exist asynchronous.
    /// </summary>
    Task<TValue> GetOrAddAsync(TKey key, Func<TKey, TValue> valueFactory);
    
    /// <summary>
    /// Adds a new key-value pair or updates an existing entry in the dictionary.
    /// </summary>
    void AddOrUpdate(TKey key, TValue value);

    /// <summary>
    /// Adds a new key-value pair or updates an existing entry in the dictionary asynchronous.
    /// </summary>
    Task AddOrUpdateAsync(TKey key, TValue value);
    
    /// <summary>
    /// Attempts to remove a key-value pair from the dictionary.
    /// </summary>
    bool TryRemove(TKey key);

    /// <summary>
    /// Attempts to remove a key-value pair from the dictionary asynchronous.
    /// </summary>
    Task<bool> TryRemoveAsync(TKey key);
    
    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    bool TryGetValue(TKey key, out TValue value);

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key asynchronous.
    /// </summary>
    Task<(bool, TValue?)> TryGetValueAsync(TKey key);
}

/// <summary>
/// Represents a concurrent cache that extends a dictionary with thread-safe caching capabilities.
/// </summary>
public interface IConcurrentXCache<TKey, TValue> : IConcurrentDictionary<TKey, TValue> { }