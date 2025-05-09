
![xCache Preview](public/xcache.jpg)
# xCache
xCache is a high-performance, thread-safe in-memory caching library written in C# 
that supports automatic expiration and concurrent access. 
It is designed for use cases where cached values need to expire 
after a given time but can be extended when accessed. This caching library was initially built for an internal project
that will be used for blockchain operations.

## Features
**Thread-Safe**: Uses `ConcurrentDictionary<TKey, TValue>` to ensure atomic updates.

**Expiring Entries**: Automatically removes old entries based on a cleanup interval.

**Extend Expiration Policy**: Allows entries to be moved to a fresh cache upon access.

**Async Support**: Provides both synchronous and asynchronous APIs.

**Optimized for Performance**: Avoids locks and ensures minimal contention

## Installation
XCache is a C# library that you can integrate into your project. To use it, add the following dependency to your project:

**NOTE**: This isn't yet published to NuGet, but can be used locally 
via cloning and adding the git source and then running:

```ps
dotnet add package xCache --version <VERSION>
```
## Expiration Mechanism
xCache implements a two-dictionary system (`newDict` and `oldDict`) to handle expiration efficiently:
* New entries are added to `newDict`.
* After expiration, `newDict` becomes `oldDict`, and a new `newDict` is created.
* If `ExtendExpirationTime` is enabled, accessing an entry in `oldDict` moves it back to `newDict`.

## Thread-Safety & Concurrency

xCache ensures safe access from multiple threads using `ConcurrentDictionary<TKey, TValue>` and `AsyncAutoResetEvent`. It guarantees:

* Atomic Reads/Writes with `ConcurrentDictionary`.
* Single-threaded cache expiration using an async cleanup task.
* No race conditions due to built-in access patterns by `ConcurrentDictionary` APIs and asynchronous wake up calls.

## Example 
```csharp Extensions.cs
public static IServiceCollection AddMemCache(this IServiceCollection services)
{
    return services.AddSingleton(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<Program>>();

        return new XCache<string, string>(
            TimeSpan.FromMinutes(2),
            CacheExpirationPolicy.ExtendExpirationTime,
            logger);
    });
}
```
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemCache();
```
```csharp
private async Task<ActionResult<string>> GenerateTokenAsync(AuthenticateUserCommand command)
{
    var (isFound, value) = await cache.TryGetValueAsync(command.Key);

    // Nonce must be present in the cache. 
    // The cache has an expiration time of about 2 minutes.
    if (!isFound || value != command.Message)
        return Unauthorized(new { error = "Unauthorized: Invalid or expired nonce" });
    
    var result = await Mediator!.Send(command);
    
    var token = result.Value; 

    return Ok(token);
}
```
```terminal
info: Program[0]
      Cleanup task started.
info: Program[0]
      Cache cleanup completed. New cache allocated. 
info: Program[0]
      xCache Added or updated an entry with key 2wRqPT7g4QfAr665pFvFDbXWp3ZnKSofMv6GsSCABAnN in the cache.
info: Program[0]
      xCache Added or updated an entry with key 2wRqPT7g4QfAr665pFvFDbXWp3ZnKSofMv6GsSCABAnN in the cache.
info: Program[0]
```

Initially the cache will delay for the very 1st request. 

First `AddOrUpdateAsync()` call:

* Waits at `await m_dictUpdatedEvent.WaitAsync();`
* It’s blocked because `m_dictUpdatedEvent.Set()` hasn’t been triggered yet.
* That `Set()` only happens after the **first cleanup cycle** (after `await Delay()` finishes).

After that first `Set()`:

* The event is signaled,
* Every future call to` AddOrUpdateAsync()` or `TryGetValueAsync()` returns immediately, without delay.


## `AsyncAutoResetEvent.cs`
We want all waiting threads to receive the same copy of `m_newDict` when it updates. `AsyncAutoResetEvent` ensures that,
but only one thread wakes up per `Set()` call. If we want all waiting threads to wake up at the same time and see the same dictionary,
we could create and use `AsyncManualResetEvent` instead. As of now, the approach implemented works fine.

### How does it work
1. First `Set()` call happens.
2. Only **1** thread is released.
This thread sees the updated dictionary (i.e., `m_newDict`).
3. Other waiting threads remain blocked. These threads still see the old dictionary reference until the next `Set()`.
4. Second `Set()` call happens.
5. Another thread wakes up and sees the same dictionary as the first thread unless `m_newDict` is updated before this `Set()`.

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

## Running Tests
All tests are provided under `test/` directory using xUnit. Feel free to add or update any tests.
x
Please follow **AAA Pattern**, and follow method the `MethodName_StateUnderTest_ExpectedBehavior` method convention.

## Contribution 
We welcome contributions to **xCache**! To ensure a smooth workflow, please follow these steps when submitting a new feature or fix.

Before making any changes, create a new branch following the naming convention:
```terminal
<feature>/<your-alias>

cache-async-impl/gio
```

* Follow the existing code style and naming conventions.
* Ensure thread safety is maintained in cache operations.
* Write tests for your changes using **xUnit**.


### Pull Request
* Provide a detailed description explaining the changes and keeping PRs
minimal and tailored to specific changes.
```angular2html
## Reason for change
...

## Problem
...

## Solution
...

## Tests
CacheTests.cs
CacheTestsTwo.cs
...
```
* Link any related GitHub Issues if applicable.
* Wait for feedback and make any requested changes.

Happy Hacking!





## Questions

*Do all concurrent dictionaries remain the same in-memory?*

* Yes, with `AsyncAutoResetEvent`, **all dictionaries** (or memory objects) remain the same until the next update and `Set()` call.
* Since only one waiting thread wakes up per `Set()` call, all other waiting threads see the same dictionary references (or memory objects) until another update occurs.

*If `ExtendExpirationTime` is enabled, accessing an entry in `oldDict` moves it back to `newDict` only if the expiration hasn't yet happened?*
* Yes. If `ExtendExpirationTime` is **enabled** and an entry is accessed while it is in `oldDict`, it moves back to `newDict`, effectively resetting its expiration time. 
  * However, if the cleanup task **has already expired the entry** (meaning it is no longer in oldDict), it cannot be revived, and the lookup will return `false`.

*Why is `m_newDict` and `m_oldDict` empty in the class but still accessible in `TryGetValueAsync`?*
* When `StartCleanUpExpiredEntriesTask()` runs, it reassigns `m_oldDict = m_newDict` and then creates a new instance for `m_newDict`.
Since `m_newDict` is reassigned to a new `ConcurrentDictionary<TKey, TValue>`, the **previous** reference that `m_oldDict` was pointing to **still exists** and is 
accessible in the `TryGetValueAsync()` method because it was captured earlier.
However, the fields in `this` (the main class instance) reflect the latest assignment where both dictionaries are reset.
So, new cache allocation does not erase old references: The old cache remains reachable until all threads referencing it finish execution.

* **Thread safety & race conditions**: If another thread accesses `TryGetValueAsync()` before the cleanup task runs, it can still retrieve values from the old dictionary.

## License

This project is open-source and available under the MIT License.


Author: Giovanny Hernandez


