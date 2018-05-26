using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using associative_cache.Interfaces;
using associative_cache.ReplacementAlgorithm.Interfaces;

namespace associative_cache
{
    public class Cache<T, U, V, W> : IDisposable
        where T : CacheEntry<V, W>
        where U : IReplacementAlgorithm<T, V, W>
    {
        private bool disposed = false;
        private bool _isOnAccessCacheEntry = false;
        private int _numSet = 0,
            _numEntry = 0;
        private readonly U _replacementFinder = default(U);

        private T[] _cacheArray;

        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();

        public Cache(int numSet, int numEntry)
        {
            // Check that the key type will work as a hashable type
            if (!typeof(IEqualityComparer<V>).IsAssignableFrom(typeof(V)))
            { throw new ArgumentException("CacheEntry<V,W> key type should implement IEqualityComparer<V> generic interface", "V"); }

            // ensure _isOnAccessCacheEntry value
            _isOnAccessCacheEntry = typeof(IOnAccessCacheEntry).IsAssignableFrom(typeof(T));

            _numEntry = numEntry;
            _numSet = numSet;
            // initialize cache to array of empty objects
            _cacheArray = (T[])Enumerable.Range(0, _numSet * _numEntry).Select(_ => new CacheEntry<V, W>()).ToArray();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                try
                {
                    if (cacheLock != null) { cacheLock.Dispose(); }
                }
                catch (Exception) { }
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public int Size
        {
            get { return _numEntry * _numSet; }
        }

        public void Clear()
        {
            // if disposed, throw exception
            EnsureNotDisposed();

            // need writelock
            cacheLock.EnterWriteLock();
            try
            {
                // setting each item to null would trigger garbage collection and take more memory
                // this may not be faster but should maintain our memory footprint, which is important
                foreach (T cache in _cacheArray)
                {
                    if (cache != null)
                        cache.IsEmpty = true;
                }
            }
            finally
            { cacheLock.ExitWriteLock(); }
        }

        public W Get(V key)
        {
            int startIndex = GetStartIndexFromKey(key);
            int endIndex = startIndex + _numEntry - 1;
            W ans = default(W);

            // need a readLock
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (_cacheArray[i] == default(T) || _cacheArray[i] == null || _cacheArray[i].IsEmpty)
                    { continue; }

                    if (((IEqualityComparer<V>)_cacheArray[i].Key).Equals(key))
                    {
                        ans = _cacheArray[i].Data;
                        // if this cache implements IOnAccessCacheEntry, 
                        //  then we need to indicate we accessed this item
                        if (_isOnAccessCacheEntry)
                        {
                            cacheLock.EnterWriteLock();
                            try
                            {
                                ((IOnAccessCacheEntry)_cacheArray[i]).OnDataAccess();
                            }
                            finally
                            { cacheLock.ExitWriteLock(); }
                        }
                    }
                }
            }
            finally
            { cacheLock.ExitUpgradeableReadLock(); }
            return ans;
        }

        public void Put(V key, W data)
        {
            int startIndex = GetStartIndexFromKey(key);
            int endIndex = startIndex + _numEntry - 1;
            bool replacing = false;
            // determine where to save the data
            int replacementIndex = GetReplacementIndex(startIndex, endIndex);

            cacheLock.EnterWriteLock();
            try
            {
                // reuse existing object to preserve memory footprint
                if (!_cacheArray[replacementIndex].Key.Equals(key) ||
                    !_cacheArray[replacementIndex].Data.Equals(data))
                { replacing = true; }
                _cacheArray[replacementIndex].Key = key;
                _cacheArray[replacementIndex].Data = data;
                _cacheArray[replacementIndex].IsEmpty = false;
                // if this cache implements IOnAccessCacheEntry, 
                //  then we need to indicate we accessed (or replaced) this item
                if (_isOnAccessCacheEntry)
                {
                    ((IOnAccessCacheEntry)_cacheArray[replacementIndex]).OnDataAccess(replacing);
                }
            }
            finally
            { cacheLock.ExitWriteLock(); }
        }

        protected void EnsureNotDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("This object has been disposed and cannot be used.");
        }

        protected int GetKeyHash(V key)
        {
            return ((IEqualityComparer<V>)key).GetHashCode();
        }

        protected int GetStartIndexFromKey(V key)
        {
            return (GetKeyHash(key) % _numSet) * _numEntry;
        }

        protected int GetReplacementIndex(int startIndex, int endIndex)
        {
            // readlock needed
            cacheLock.EnterReadLock();
            try
            {
                // first check if we have an empty block to use
                int ans = GetFirstEmptyIndex(startIndex, endIndex);
                // if so use that for the put
                if (ans > -1) return ans;

                // otherwise we'll need to use our IReplacementAlgorithm to figure out which block to use
                // Using ArraySegment wrapped in a ReadOnlyCollection we can referencially pass the set
                // we need for full inspection quickly without increasing memory footprint too much, since
                // we're not copying elements, but we don't risk the consumer trying to edit the cache (that's our job)
                ans = _replacementFinder.GetReplacementIndex(
                    new ReadOnlyCollection<T>(
                        (IList<T>)new ArraySegment<T>(_cacheArray, startIndex, endIndex - startIndex + 1)));

                // Make sure replacement strategy didn't do something crazy
                if (ans < 0 || ans > (endIndex - startIndex))
                { throw new ArgumentException(string.Format("IReplacementAlgorithm ({0}) returned invalid index value.", typeof(U).GetType().FullName), "U"); }

                // determine index in full cache
                return startIndex = ans;
            }
            finally
            { cacheLock.ExitReadLock(); }
        }

        protected int GetFirstEmptyIndex(int startIndex, int endIndex)
        {
            // theoretically we should already be ReadLocked at this point,
            // but for safety, and because read locks can run in parallel...
            cacheLock.EnterReadLock();
            try
            {
                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (_cacheArray[i] == null || _cacheArray[i].IsEmpty)
                    { return i; }
                }
                return -1;
            }
            finally { cacheLock.ExitReadLock(); }
        }
    }
}