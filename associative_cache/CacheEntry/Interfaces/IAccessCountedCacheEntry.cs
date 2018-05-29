namespace associative_cache.Interfaces
{
    /// <summary>
    /// Interface for cache objects that need to track how many times they are accessed
    /// </summary>
     public interface IAccessCountedCacheEntry : IOnAccessCacheEntry
    {
        /// <value>
        /// Gets an <c>Int32</c> representing the amount of times this cache object has been accessed
        /// </value>
        int AccessCount { get; }
    }
}