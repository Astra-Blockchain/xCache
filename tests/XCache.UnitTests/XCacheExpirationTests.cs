using Microsoft.Extensions.Logging.Abstractions;
using xCacheLibrary;

namespace XCache.UnitTests;

public class XCacheExpirationTests
{
    private XCache<int, string> CreateCache(TimeSpan expirationTime, CacheExpirationPolicy policy)
        => new XCache<int, string>(
            maxExpirationTime: expirationTime, 
            cacheExpirationPolicy: policy,
            logger: new NullLogger<XCache<int, string>>());

    [Fact]
    public async Task Cache_ShouldExpireAfterHalfExpirationTime_ReturnsIfFoundBeforeAndAfterExpiration()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMilliseconds(500), CacheExpirationPolicy.ExtendExpirationTime);
        cache.AddOrUpdate(1, "Hello");
        
        // Act
        await Task.Delay(100);
        bool foundBeforeExpiry = cache.TryGetValue(1, out _);

        await Task.Delay(500);
        bool foundAfterExpiry = cache.TryGetValue(1, out _);

        // Assert
        Assert.True(foundBeforeExpiry);
        Assert.False(foundAfterExpiry);
    }
    
    [Fact]
    public async Task Cache_ShouldResetAfterExpiration_ReturnsIfFoudBeforeAndAfterExpiration()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMilliseconds(1000), CacheExpirationPolicy.ExtendExpirationTime);
        cache.AddOrUpdate(1, "A");
        cache.AddOrUpdate(2, "B");

        // Act
        await Task.Delay(100);
        bool found1 = cache.TryGetValue(1, out _);
        bool found2 = cache.TryGetValue(2, out _);

        await Task.Delay(1000);
        bool foundAfterExpiry = cache.TryGetValue(1, out _);

        // Assert
        Assert.True(found1);
        Assert.True(found2);
        Assert.False(foundAfterExpiry);
    }
}