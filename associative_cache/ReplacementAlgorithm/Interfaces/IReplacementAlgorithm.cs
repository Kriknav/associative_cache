
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace associative_cache.ReplacementAlgorithm.Interfaces
{
    /// <summary>
    /// Interface used to implement new replacement algorithm logic for <c>Cache</c> objects
    /// </summary>
    /// <typeparam name="T">The type of <see cref="CacheEntry{U,V}"/> objects this replacement algorithm will work on</typeparam>
    /// <typeparam name="U">The type of keys used in the <see cref="CacheEntry{U,V}"/> object</typeparam>
    /// <typeparam name="V">The type of data used in the <see cref="CacheEntry{U,V}"/> object</typeparam>
    public interface IReplacementAlgorithm<T, U, V>
        where T : CacheEntry<U, V>
    {
        /// <summary>
        /// Determines the index in a given set that should be replaced
        /// </summary>
        /// <param name="set">The set of <see cref="CacheEntry{U,V}"/> objects as a <see cref="ReadOnlyCollection{T}"/> </param>
        /// <returns>An <c>int</c> indicating the index of the object to be replaced, should be between 0 and <c>set.Count - 1</c></returns>
        int GetReplacementIndex(ReadOnlyCollection<T> set);
    }
}