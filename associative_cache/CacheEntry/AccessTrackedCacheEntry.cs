using System;
using associative_cache.Interfaces;

namespace associative_cache
{
    public class AccessTrackedCacheEntry<T, U> : CacheEntry<T, U>, IAccessCountedCacheEntry, IAccessTimestampedCacheEntry, IOnAccessCacheEntry
    {
        /// <value>
        /// Gets an <c>Int32</c> representing the amount of times this cache object has been accessed
        /// </value>
        public int AccessCount { get; protected set; } = 1; // start at one since creation is considered access

        /// <value>
        /// Gets the <c>DateTime</c> timestamp of the last time this object was accessed in the cache
        /// </value>
        public DateTime Timestamp { get; protected set; } = DateTime.MinValue.ToUniversalTime();

        /// <summary>
        /// Performs logic needed when <c>AccessCountedCacheEntry</c> object is accessed in the cache
        /// </summary>
        /// <remarks>
        /// NOTE: this method assumes the access was due to a cache read
        /// </remarks>
        public void OnDataAccess()
        {
           OnDataAccess(false);
        }

        /// <summary>
        /// Performs logic needed when <c>AccessCountedCacheEntry</c> object is accessed in the cache based on supplied method of access
        /// </summary>
        /// <param name="newValues">Boolean that is <c>true</c> of the cache believes this object is being written new or replacing an old cache object, <c>false</c> otherwise</param>
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