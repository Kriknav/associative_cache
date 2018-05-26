namespace associative_cache.Interfaces
{
     public interface IAccessCountedCacheEntry : IOnAccessCacheEntry, ICacheable
    {
        int AccessCount { get; }
    }
}