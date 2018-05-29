using System;

namespace associative_cache.Interfaces
{
    /// <summary>
    /// Interface for cache objects that need to track the last time they were accessed
    /// </summary>
     public interface IAccessTimestampedCacheEntry : IOnAccessCacheEntry
    {
        /// <value>
        /// Gets the <c>DateTime</c> timestamp of the last time this object was accessed in the cache
        /// </value>
        DateTime Timestamp { get; }
    }
}