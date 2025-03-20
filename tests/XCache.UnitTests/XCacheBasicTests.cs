using Microsoft.Extensions.Logging.Abstractions;
using xCacheLibrary;

namespace XCache.UnitTests;

public class XCacheBasicTests
{
    private XCache<int, string> CreateCache(TimeSpan expirationTime, CacheExpirationPolicy policy) 
        => new XCache<int, string>(
            maxExpirationTime: expirationTime, 
            cacheExpirationPolicy: policy,
            logger: new NullLogger<XCache<int, string>>());

    [Fact]
    public void AddOrUpdate_ShouldStoreEntry_ReturnsTrue()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMinutes(10), CacheExpirationPolicy.ExtendExpirationTime);
        
        // Act
        cache.AddOrUpdate(1, "Hello");
        bool found = cache.TryGetValue(1, out string value);

        // Assert
        Assert.True(found);
        Assert.Equal("Hello", value);
    }
    
    [Fact]
    public void TryRemove_ShouldRemoveEntry_ReturnsFalse()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMinutes(10), CacheExpirationPolicy.ExtendExpirationTime);
        cache.AddOrUpdate(1, "ToRemove");

        // Act
        bool removed = cache.TryRemove(1);
        bool found = cache.TryGetValue(1, out _);

        // Assert
        Assert.True(removed);
        Assert.False(found);
    }
}