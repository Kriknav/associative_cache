
using System.Collections.Generic;

namespace associative_cache.ReplacementAlgorithm.Interfaces
{
    public interface IReplacementAlgorithm<T, U, V>
        where T : CacheEntry<U, V>
    {
        int GetReplacementIndex(IList<T> set);
    }
}