using Microsoft.Extensions.Logging;

namespace xCacheLibrary;

public class XCache<TKey, TValue> : IConcurrentXCache<TKey, TValue>, IDisposable
{
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, ILogger logger)
    {
        throw new NotImplementedException();
    }

    public void AddOrUpdate(TKey key, TValue value, ILogger logger)
    {
        throw new NotImplementedException();
    }

    public bool TryRemove(TKey key, ILogger logger)
    {
        throw new NotImplementedException();
    }

    public bool TryGetValue(TKey key, out TValue value, ILogger logger)
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}