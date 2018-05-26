namespace associative_cache.Interfaces
{
    public interface IOnAccessCacheEntry
    {
        void OnDataAccess();

        void OnDataAccess(bool newValues);
    }
}