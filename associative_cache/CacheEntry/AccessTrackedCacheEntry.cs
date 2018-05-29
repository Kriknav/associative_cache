using System;
using associative_cache.Interfaces;

namespace associative_cache
{
    public class AccessTrackedCacheEntry<T, U> : CacheEntry<T, U>, IAccessCountedCacheEntry, IAccessTimestampedCacheEntry, IOnAccessCacheEntry
    {
        /// <value>
        /// Tracks how many times this <c>AccessCountedCacheEntry</c> object has been accessed in the cache
        /// </value>
        public int AccessCount { get; protected set; } = 1; // start at one since creation is considered access

        public DateTime Timestamp { get; protected set; } = DateTime.MinValue.ToUniversalTime();

        /// <summary>
        /// Performs functions needed when <c>AccessCountedCacheEntry</c> object is accessed in the cache
        /// </summary>
        public void OnDataAccess()
        {
           OnDataAccess(false);
        }

        public void OnDataAccess(bool newValues)
        {
            // if not updating just increment count
            if (!newValues)
            {  AccessCount++; }
            else
            {
                // otherwise we need to reset the access count cause this is a 'new' cached object
                AccessCount = 1;
            }
            // always update the timestamp
            Timestamp = DateTime.UtcNow;
        }
    }
}