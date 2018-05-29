using System;

namespace associative_cache.Interfaces
{
     public interface IAccessTimestampedCacheEntry : IOnAccessCacheEntry
    {
        DateTime Timestamp { get; }
    }
}