using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using associative_cache.ReplacementAlgorithm.Interfaces;

namespace associative_cache.ReplacementAlgorithm
{
    /// <summary>
    /// Implementation of the Least Recently Used (LRU) replacement algorithm for <c>Cache</c> objects
    /// </summary>
    /// <typeparam name="T">The type of <see cref="AccessTrackedCacheEntry{U,V}"/> objects this replacement algorithm will work on</typeparam>
    /// <typeparam name="U">The type of keys used in the <see cref="AccessTrackedCacheEntry{U,V}"/> object</typeparam>
    /// <typeparam name="V">The type of data used in the <see cref="AccessTrackedCacheEntry{U,V}"/> object</typeparam>
    public class LeastRecentlyUsed<T, U, V> : IReplacementAlgorithm<T, U, V>
        where T : AccessTrackedCacheEntry<U, V>
    {
        /// <summary>
        /// Determines and returns the index of the least recently used item in the <c>set</c>
        /// </summary>
        /// <param name="set">The set of <see cref="AccessTrackedCacheEntry{U,V}"/> objects as a <see cref="ReadOnlyCollection{T}"/> </param>
        /// <returns>The index of the least recently used object in the set as an <c>int</c></returns>
        public int GetReplacementIndex(ReadOnlyCollection<T> set)
        {
            int ans = -1,
                min = Int32.MaxValue;
            DateTime minTime = DateTime.MaxValue.ToUniversalTime();
            for (int i = 0; i < set.Count; i++)
            {
                if (set[i].AccessCount < min || 
                    (set[i].AccessCount == min && set[i].Timestamp < minTime))
                {
                    ans = i;
                    min = set[i].AccessCount;
                    minTime = set[i].Timestamp;
                }
            }

            return ans;
        }
    }
}