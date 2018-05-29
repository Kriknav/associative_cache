namespace associative_cache
{
    /// <summary>
    /// Interface for cache objects defining the key and data types
    /// </summary>
    /// <typeparam name="T">The type of keys used in the cache</typeparam>
    /// <typeparam name="U">The type of data stored in the cache</typeparam>
    public interface ICache<T, U> 
    {
        /// <summary>
        /// Returns the total size of the cache
        /// </summary>
        /// <returns>The total size of the cache, <c>numberOfSets * numberOfEntries</c> as an <c>int</c></returns>
        int Size { get; }

        /// <summary>
        /// Ensures all objects in the cache are marked as empty so they can be reused
        /// </summary>
        void Clear();

        /// <summary>
        /// Uses the provided key to lookup data in the cache and returns it
        /// </summary>
        /// <param name="key">The key used to lookup data in the cache</param>
        /// <returns>If the key is found, returns the data associated with it, otherwise <c>default(U)</c></returns>
        U Get(T key);

        /// <summary>
        /// Stores the provided data in the cache using the key to determine its destination set
        /// </summary>
        /// <param name="key">The key used to store the data in the cache, determines the data's set as well as association for lookups using <see cref="ICache{T,U}.Get(T)"/></param>
        /// <param name="data">The data to be stored in the cache</param>
        void Put(T key, U data);
    }
}