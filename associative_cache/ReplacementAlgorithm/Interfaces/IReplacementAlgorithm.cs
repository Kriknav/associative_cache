
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace associative_cache.ReplacementAlgorithm.Interfaces
{
    public interface IReplacementAlgorithm<T, U, V>
        where T : CacheEntry<U, V>
    {
        int GetReplacementIndex(ReadOnlyCollection<T> set);
    }
}