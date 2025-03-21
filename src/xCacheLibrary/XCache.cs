// --------------------------------------------------------
// Copyright (c) Astra. All rights reserved.
// 
// Author:  Giovanny Hernandez
// Created: March 19, 2025
// --------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace xCacheLibrary;

public class XCache<TKey, TValue> : IConcurrentXCache<TKey, TValue>, IDisposable
{
    private volatile ConcurrentDictionary<TKey, TValue> m_oldDict;
    private volatile ConcurrentDictionary<TKey, TValue> m_newDict;
    private volatile bool m_disposed;

    private readonly AsyncAutoResetEvent m_dictUpdatedEvent = new AsyncAutoResetEvent();
    private readonly CacheExpirationPolicy m_cacheExpirationPolicy;
    private readonly IEqualityComparer<TKey> m_keyComparer;
    private readonly TimeSpan m_expirationTime;
    private readonly ILogger m_logger;
    private readonly string m_cacheName;

    public XCache(
        TimeSpan maxExpirationTime, 
        CacheExpirationPolicy cacheExpirationPolicy, 
        ILogger logger,
        string cacheName = "", 
        IEqualityComparer<TKey>? comparer = null
        )
    {
        m_expirationTime = maxExpirationTime;
        m_cacheExpirationPolicy = cacheExpirationPolicy;
        m_logger = logger;
        m_cacheName = string.IsNullOrEmpty(cacheName) ? "xCache" : cacheName;
        m_keyComparer = comparer ?? EqualityComparer<TKey>.Default;
        m_expirationTime = TimeSpan.FromMilliseconds(maxExpirationTime.TotalMilliseconds / 2);
        m_disposed = false;
        
        InitConcurrentDictionaries();
        
        // CPU bound work, ask the thread pool to put it on another thread.
        // This ensures the cleanup process runs in the background without blocking the constructor.
        // There's no expected exceptions which it is not a concern if an exception is lost.
        _ = Task.Run(async () => await StartCleanUpExpiredEntriesTask());
    }

    private void InitConcurrentDictionaries()
    {
        m_oldDict = new ConcurrentDictionary<TKey, TValue>(m_keyComparer);
        m_newDict = new ConcurrentDictionary<TKey, TValue>(m_keyComparer); 
    }
    
    /// <summary>
    /// Synchronously retrieves references to the dictionaries. It does not need extra threading mechanisms like locks or memory barriers.
    /// Using volatile ensures visibility, so other threads always see the latest m_newDict and m_oldDict values. Therefore, cache lookups are
    /// inherently thread-safe 
    /// </summary>
    private void GetConcurrentDictionaryRefs(out ConcurrentDictionary<TKey, TValue> newDictRef, out ConcurrentDictionary<TKey, TValue> oldDictRef)
    {
        // No need for locks, Thread.MemoryBarrier(), or Interlocked operations.
        // No race conditions since dictionaries are only being read.
        newDictRef = m_newDict;
        oldDictRef = m_oldDict;
    }

    /// <summary>
    /// Safely retrieve references to the two dictionaries. This is needed for async workflows where threads must wait for cache updates.
    /// </summary>
    private async Task<(ConcurrentDictionary<TKey, TValue>, ConcurrentDictionary<TKey, TValue>)> GetConcurrentDictionaryRefsAsync()
    {
        await m_dictUpdatedEvent.WaitAsync(); // Wait for an update (only one thread wakes up)
    
        return (m_newDict, m_oldDict);
    }
    
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        if (TryGetValue(key, out TValue existingValue))
            return existingValue; 

        GetConcurrentDictionaryRefs(out var newDictRef, out var oldDictRef);

        bool isInOldDict = false, isValueAssignedFromValueFactory = false;

        TValue value = newDictRef.GetOrAdd(key, _ =>
        {
            // Promote value from old cache if available
            if (oldDictRef.TryGetValue(key, out TValue oldValue))
            {
                isInOldDict = true;
                return oldValue;
            }

            // Compute only if necessary: GetConcurrentDictionaryRefsAsync() method ensures that only 1 thread wakes up at
            // a time when waiting for an update, but it does not prevent multiple threads from entering GetOrAdd() at the same time.
            isValueAssignedFromValueFactory = true;
            return valueFactory(key);
        });

        oldDictRef.TryRemove(key, out _);
        
        if (isValueAssignedFromValueFactory)
        {
            m_logger.LogInformation("{CacheName} Added an entry with key: {Key} to the cache.", m_cacheName, key);
        } 
        else if (isInOldDict)
        {
            m_logger.LogInformation("{CacheName} Found key: {Key} from the old cache.", m_cacheName, key);
        }

        return value;
    }
    
    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, TValue> valueFactory)
    {
        var (newDictRef, oldDictRef) = await GetConcurrentDictionaryRefsAsync();

        bool isInOldDict = false, isValueAssignedFromValueFactory = false;

        TValue value = newDictRef.GetOrAdd(key, _ =>
        {
            // Extra check
            if (oldDictRef.TryGetValue(key, out TValue oldValue))
            {
                isInOldDict = true;
                return oldValue; 
            }

            // Only one thread will compute valueFactory(key)
            isValueAssignedFromValueFactory = true;
            return valueFactory(key);
        });

        oldDictRef.TryRemove(key, out _);
        
        // Notify that the cache has changed
        m_dictUpdatedEvent.Set();
        
        if (isValueAssignedFromValueFactory)
        {
            m_logger.LogInformation("{CacheName} Added an entry with key: {Key} to the cache.", m_cacheName, key);
        } 
        else if (isInOldDict)
        {
            m_logger.LogInformation("{CacheName} Found key: {Key} from the old cache.", m_cacheName, key);
        }

        return value;
    }
    
    public void AddOrUpdate(TKey key, TValue value)
    {
        GetConcurrentDictionaryRefs(out var newDictRef, out var oldDictRef);
        
        // Always update the new cache
        newDictRef[key] = value;
        
        // Remove key from old cache
        oldDictRef.TryRemove(key, out _);
        
        m_logger.LogInformation("{CacheName} Added or updated an entry with key {Key} in the cache.", m_cacheName, key);
    }
    
    public async Task AddOrUpdateAsync(TKey key, TValue value)
    {
        var (newDictRef, oldDictRef) = await GetConcurrentDictionaryRefsAsync();

        // Always update the new cache
        newDictRef[key] = value;

        // Remove key from old cache
        oldDictRef.TryRemove(key, out _);
        
        // Notify that the cache has changed
        m_dictUpdatedEvent.Set();

        m_logger.LogInformation("{CacheName} Added or updated an entry with key {Key} in the cache.", m_cacheName, key);
    }

    private bool TryRemove(TKey key, out TValue value)
    {
        value = default;
        
        GetConcurrentDictionaryRefs(out var newDictRef, out var oldDictRef);
        
        bool removedFromOld = oldDictRef.TryRemove(key, out TValue retrievedValue);
        bool removedFromNew = newDictRef.TryRemove(key, out TValue retrievedValueNew);
    
        bool removed = removedFromOld || removedFromNew;
        
        if (removedFromOld)
        {
            value = retrievedValue;
        }
        else if (removedFromNew)
        {
            value = retrievedValueNew;
        }

        if (!removed)
        {
            m_logger.LogInformation("{CacheName}: Entry with key {Key} was not found in the cache.", m_cacheName, key);
        }
        else
        {
            m_logger.LogInformation("{CacheName}: Removed an entry with key: {Key} from the cache.", m_cacheName, key);
        }

        return removed;
    }
    
    private async Task<(bool, TValue?)> TryRemoveWithValueAsync(TKey key)
    {
        var (newDictRef, oldDictRef) = await GetConcurrentDictionaryRefsAsync();

        bool removedFromOld = oldDictRef.TryRemove(key, out TValue retrievedValue);
        bool removedFromNew = newDictRef.TryRemove(key, out TValue retrievedValueNew);

        bool removed = removedFromOld || removedFromNew;
        TValue? value = removed ? (removedFromNew ? retrievedValueNew : retrievedValue) : default;

        if (!removed)
        {
            m_logger.LogInformation("{CacheName}: Entry with key {Key} was not found in the cache.", m_cacheName, key);
        }
        else
        {
            // Notify that the cache has changed
            m_dictUpdatedEvent.Set();
            
            m_logger.LogInformation("{CacheName}: Removed an entry with key: {Key} from the cache.", m_cacheName, key);
        }

        return (removed, value);
    }
    
    /// <summary>
    /// Synchronous wrapper for TryRemove.
    /// </summary>
    public bool TryRemove(TKey key)
    {
        return TryRemove(key, out _); // Discard the removed value
    }

    /// <summary>
    /// Asynchronous wrapper for TryRemoveAsync.
    /// </summary>
    public async Task<bool> TryRemoveAsync(TKey key)
    {
        var (removed, _) = await TryRemoveWithValueAsync(key);
        return removed;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        GetConcurrentDictionaryRefs(out var newDictRef, out var oldDictRef);

        // Check the new cache
        if (newDictRef.TryGetValue(key, out value))
        {
            m_logger.LogInformation("{CacheName} TryGetValue(): Entry with key {Key} found in new cache.", m_cacheName, key);
            return true;
        }

        // Otherwise the old cache
        if (oldDictRef.TryGetValue(key, out value))
        {
            if (m_cacheExpirationPolicy == CacheExpirationPolicy.ExtendExpirationTime)
            {
                // Move to new dictionary to extend its life
                value = newDictRef.GetOrAdd(key, value);
                oldDictRef.TryRemove(key, out _);

                m_logger.LogInformation("{CacheName} TryGetValue(): Entry with key {Key} found in old cache and its expiration was extended.", m_cacheName, key);
            }
            else
            {
                m_logger.LogInformation("{CacheName} TryGetValue(): Entry with key {Key} found in old cache.", m_cacheName, key);
            }
            return true;
        }
        
        m_logger.LogInformation("{CacheName} TryGetValue(): Entry with key {Key} not found in the cache.", m_cacheName, key);
        
        value = default;
        
        return false;
    }

    public async Task<(bool, TValue?)> TryGetValueAsync(TKey key)
    {
        var (newDictRef, oldDictRef) = await GetConcurrentDictionaryRefsAsync();

        // Check the new cache
        if (newDictRef.TryGetValue(key, out TValue value))
        {
            m_logger.LogInformation("{CacheName} TryGetValueAsync(): Entry with key {Key} found in new cache.", m_cacheName, key);
            return (true, value);
        }

        // Check the old cache
        if (oldDictRef.TryGetValue(key, out value))
        {
            if (m_cacheExpirationPolicy == CacheExpirationPolicy.ExtendExpirationTime)
            {
                // Move to new dictionary to extend its life
                value = newDictRef.GetOrAdd(key, value);
                oldDictRef.TryRemove(key, out _);

                m_logger.LogInformation("{CacheName} TryGetValueAsync(): Entry with key {Key} found in old cache and its expiration was extended.", m_cacheName, key);
            }
            else
            {
                m_logger.LogInformation("{CacheName} TryGetValueAsync(): Entry with key {Key} found in old cache.", m_cacheName, key);
            }
            return (true, value);
        }

        m_logger.LogInformation("{CacheName} TryGetValueAsync(): Entry with key {Key} not found in the cache.", m_cacheName, key);

        return (false, value);
    }

    /// <summary>
    /// Starts the background cleanup task to expire the cache at fixed intervals.
    /// </summary>
    private async Task StartCleanUpExpiredEntriesTask()
    {
        while (!m_disposed)
        {
            await Delay();
            
            m_oldDict = m_newDict;

            // NOTE: This will allocate a new dictionary even if the cache is empty.
            m_newDict = new ConcurrentDictionary<TKey, TValue>(m_keyComparer);
            
            // Notify that the cache has changed
            m_dictUpdatedEvent.Set();

            m_logger.LogInformation("Cache cleanup completed. New cache allocated.");
        }
    }

    private async Task Delay() 
        => await Task.Delay(m_expirationTime);
    
    public void Dispose()
    {
        if (m_disposed) return;

        m_oldDict.Clear();
        m_newDict.Clear();
        m_disposed = true;
    }
}

public enum CacheExpirationPolicy
{
    ExtendExpirationTime,
    DontExtendExpirationTime
}