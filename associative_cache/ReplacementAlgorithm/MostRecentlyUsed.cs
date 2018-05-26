using System;
using System.Collections.Generic;
using associative_cache.ReplacementAlgorithm.Interfaces;

namespace associative_cache.ReplacementAlgorithm
{
    public class MostRecentlyUsed<T, U, V> : IReplacementAlgorithm<T, U, V>
        where T : AccessTrackedCacheEntry<U, V>
    {
        public int GetReplacementIndex(IList<T> set)
        {
            int ans = -1,
                max = 0;
            DateTime maxTime = DateTime.MinValue.ToUniversalTime();
            for (int i = 0; i < set.Count; i++)
            {
                if (set[i].AccessCount > max || 
                    (set[i].AccessCount == max && set[i].Timestamp > maxTime))
                {
                    ans = i;
                    max = set[i].AccessCount;
                    maxTime = set[i].Timestamp;
                }
            }

            return ans;
        }
    }
}