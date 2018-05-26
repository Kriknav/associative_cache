using System;

namespace associative_cache.Interfaces
{
     public interface IAccessTimestampedCacheEntry : IOnAccessCacheEntry, ICacheable
    {
        DateTime Timestamp { get; }
    }
}