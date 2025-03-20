using Microsoft.Extensions.Logging.Abstractions;
using xCacheLibrary;

namespace XCache.UnitTests;

public class XCacheConcurrencyTests
{
    private XCache<int, string> CreateCache(TimeSpan expirationTime, CacheExpirationPolicy policy) 
        => new XCache<int, string>(
            maxExpirationTime: expirationTime, 
            cacheExpirationPolicy: policy,
            logger: new NullLogger<XCache<int, string>>());
    
    [Fact]
    public async Task MultipleThreads_ShouldReceiveUpdatesAfterSet_ReturnsTrueIfValueFound()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMilliseconds(500), CacheExpirationPolicy.ExtendExpirationTime);
        cache.AddOrUpdate(1, "Initial");

        var task1 = Task.Run(async () =>
        {
            await Task.Delay(200);
            cache.AddOrUpdate(1, "Updated");
        });

        var task2 = Task.Run(async () =>
        {
            await Task.Delay(300);
            var (found, value) = await cache.TryGetValueAsync(1);
            Assert.True(found);
            Assert.Equal("Updated", value);
        });

        /*
           Time (ms)  | Action
           -----------|----------------------------------------
           0          | Add key (1 → "Initial") to cache.
           200        | Update key (1 → "Updated") in cache.
           300        | Read key (1) → Expect "Updated".
           
         */
        await Task.WhenAll(task1, task2);
    }
    
    [Fact]
    public async Task TryRemoveAsync_ShouldWakeUpWaitingThreads_ReturnsFalseAfterValueRemoval()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMilliseconds(500), CacheExpirationPolicy.ExtendExpirationTime);
        cache.AddOrUpdate(1, "Entry");

        var removeTask = Task.Run(async () =>
        {
            await Task.Delay(200);
            await cache.TryRemoveAsync(1);
        });

        var checkTask = Task.Run(async () =>
        {
            await Task.Delay(300);
            var (found, _) = await cache.TryGetValueAsync(1);
            Assert.False(found);
        });

        await Task.WhenAll(removeTask, checkTask);
    }
}