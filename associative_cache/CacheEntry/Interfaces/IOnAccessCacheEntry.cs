namespace associative_cache.Interfaces
{
    /// <summary>
    /// Interface for cache objects that need to perform logic when accessed in the cache
    /// </summary>
    public interface IOnAccessCacheEntry
    {
        /// <summary>
        /// Basic method that performs logic when accessed in the cache
        /// </summary>
        void OnDataAccess();
        
        /// <summary>
        /// Method of performing logic when accessed in the cache, logic can be different if the access was due to a read or a write
        /// </summary>
        /// <param name="newValues">Boolean that is <c>true</c> of the cache believes this object is being written new or replacing an old cache object, <c>false</c> otherwise</param>
        void OnDataAccess(bool newValues);
    }
}