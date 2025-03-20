# xCache
A Memcache Library for .NET. This is a thread safe non-blocking and scalable in-memory cache.

## `xCache.cs`


## `AsyncAutoResetEvent.cs`
We want all waiting threads to receive the same copy of `m_newDict` when it updates. `AsyncAutoResetEvent` ensures that,
but only one thread wakes up per `Set()` call. If we want all waiting threads to wake up at the same time and see the same dictionary,
we could create and use `AsyncManualResetEvent` instead.

### How does it work
1. First `Set()` call happens
2. Only **1** thread is released.
This thread sees the updated dictionary (i.e., `m_newDict`).
3. Other waiting threads remain blocked. These threads still see the old dictionary reference until the next `Set()`.
Second `Set()` call happens
4. Another thread wakes up and sees the same dictionary as the first thread unless _newDict is updated before this `Set()`.

### Mechanism 
```csharp Program.cs
var cacheUpdatedEvent = new AsyncAutoResetEvent();

async Task Worker(string name)
{
    Console.WriteLine($"{name} waiting for cache update...");
    await cacheUpdatedEvent.WaitAsync();
    Console.WriteLine($"{name} sees new dictionary: {_newDict}");
}

async Task UpdateCache()
{
    await Task.Delay(1000); // Simulate async update work
    _newDict = new ConcurrentDictionary<int, string>(); // Change the dictionary
    Console.WriteLine("Cache updated, signaling event...");
    cacheUpdatedEvent.Set(); // Notify only ONE thread
}

async Task Main()
{
    var task1 = Worker("Thread 1");
    var task2 = Worker("Thread 2");
    var task3 = Worker("Thread 3");

    await UpdateCache();
    await Task.Delay(500);

    Console.WriteLine("Signaling event again...");
    cacheUpdatedEvent.Set(); // Notify next thread

    await Task.WhenAll(task1, task2, task3);
}

await Main();
```
```terminal
Thread 1 waiting for cache update...
Thread 2 waiting for cache update...
Thread 3 waiting for cache update...
Cache updated, signaling event...
Thread 1 sees new dictionary: <new dictionary>
Signaling event again...
Thread 2 sees new dictionary: <same dictionary>
(Thread 3 is still waiting)
```

## Questions

*Do all concurrent dictionaries remain the same in-memory*?

* Yes, with `AsyncAutoResetEvent`, **all dictionaries** (or memory objects) remain the same until the next update and `Set()` call.
* Since only one waiting thread wakes up per `Set()` call, all other waiting threads see the same dictionary references (or memory objects) until another update occurs.

Why is `m_newDict` and `m_oldDict` empty in the class but still accessible in `TryGetValueAsync`?*
* When `StartCleanUpExpiredEntriesTask()` runs, it reassigns `m_oldDict = m_newDict` and then creates a new instance for `m_newDict`.
Since `m_newDict` is reassigned to a new `ConcurrentDictionary<TKey, TValue>`, the **previous** reference that `m_oldDict` was pointing to **still exists** and is 
accessible in the `TryGetValueAsync()` method because it was captured earlier.
However, the fields in `this` (the main class instance) reflect the latest assignment where both dictionaries are reset.
So, new cache allocation does not erase old references: The old cache remains reachable until all threads referencing it finish execution.

* **Thread safety & race conditions**: If another thread accesses `TryGetValueAsync()` before the cleanup task runs, it can still retrieve values from the old dictionary.




