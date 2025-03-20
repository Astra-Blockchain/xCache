using Microsoft.Extensions.Logging.Abstractions;
using xCacheLibrary;

namespace XCache.UnitTests;

public class XCacheAsyncTests
{
    private XCache<int, string> CreateCache(TimeSpan expirationTime, CacheExpirationPolicy policy) 
        => new XCache<int, string>(
            maxExpirationTime: expirationTime, 
            cacheExpirationPolicy: policy,
            logger: new NullLogger<XCache<int, string>>());
    
    [Fact]
    public async Task TryGetValueAsync_ShouldStoreEntry_ReturnsEntryFromNewCache()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMilliseconds(500), CacheExpirationPolicy.ExtendExpirationTime);
        cache.AddOrUpdate(1, "NewEntry");

        // Act
        var (found, value) = await cache.TryGetValueAsync(1);

        // Assert
        Assert.True(found);
        Assert.Equal("NewEntry", value);
    }

    [Fact]
    public async Task TryGetValueAsync_ShouldPromoteEntryIfExtendExpirationTimeEnabled_ReturnsEntry()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMilliseconds(500), CacheExpirationPolicy.ExtendExpirationTime);
        cache.AddOrUpdate(1, "OldEntry");

        // Wait for it to move to the old cache
        // The cache hasn't fully expired yet, so the entry moves from m_newDict → m_oldDict.
        await Task.Delay(300);

        // Act
        var (found, value) = await cache.TryGetValueAsync(1);

        // Assert
        Assert.True(found);
        Assert.Equal("OldEntry", value);
    }

    [Fact]
    public async Task TryGetValueAsync_ShouldNotPromoteEntryIfDontExtendExpirationTime_ReturnEntry()
    {
        // Arrange
        var cache = CreateCache(TimeSpan.FromMilliseconds(500), CacheExpirationPolicy.DontExtendExpirationTime);
        cache.AddOrUpdate(1, "OldEntry");

        await Task.Delay(300);
        var (found, value) = await cache.TryGetValueAsync(1);

        Assert.True(found);
        Assert.Equal("OldEntry", value);

        await Task.Delay(300);
        var (foundAfterExpiry, _) = await cache.TryGetValueAsync(1);

        Assert.False(foundAfterExpiry);
    }
}