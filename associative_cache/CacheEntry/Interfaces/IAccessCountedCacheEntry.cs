namespace associative_cache.Interfaces
{
     public interface IAccessCountedCacheEntry : IOnAccessCacheEntry
    {
        int AccessCount { get; }
    }
}