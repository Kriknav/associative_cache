using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using associative_cache.ReplacementAlgorithm.Interfaces;

namespace associative_cache.ReplacementAlgorithm
{
    public class LeastRecentlyUsed<T, U, V> : IReplacementAlgorithm<T, U, V>
        where T : AccessTrackedCacheEntry<U, V>
    {
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