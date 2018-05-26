namespace associative_cache
{
    public interface ICache<T, U> 
    {
        int Size { get; }

        void Clear();

        U Get(T key);

        void Put(T key, U data);
    }
}