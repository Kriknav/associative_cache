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
    public class Cache<TItem, UKey, VData> : IDisposable, ICache<UKey, VData>
        where TItem : CacheEntry<UKey, VData>, new()
    {
        private bool disposed = false;
        private bool _isOnAccessCacheEntry = false;
        private int _numSet = 0,
            _numEntry = 0;
        private readonly IReplacementAlgorithm<TItem, UKey, VData> _replacementFinder = null;

        private TItem[] _cacheArray;

        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();

        public Cache(int numSet, int numEntry, IReplacementAlgorithm<TItem, UKey, VData> replacementAlgo)
        {
            // Check that the key type will work as a hashable type
            if (!typeof(IEquatable<UKey>).IsAssignableFrom(typeof(UKey)))
            { throw new ArgumentException("CacheEntry<UKey,VData> key type should implement IEquatable<UKey> generic interface", "UKey"); }

            // ensure _isOnAccessCacheEntry value
            _isOnAccessCacheEntry = typeof(IOnAccessCacheEntry).IsAssignableFrom(typeof(TItem));

            _numEntry = numEntry;
            _numSet = numSet;
            // initialize cache to array of empty objects
            _cacheArray = Enumerable.Range(0, _numSet * _numEntry).Select(_ => new TItem()).ToArray();

            // setup replacement algorithm
            _replacementFinder = replacementAlgo;
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
                foreach (TItem cache in _cacheArray)
                {
                    if (cache != null)
                        cache.IsEmpty = true;
                }
            }
            finally
            { cacheLock.ExitWriteLock(); }
        }

        public VData Get(UKey key)
        {
            // if disposed, throw exception
            EnsureNotDisposed();

            int startIndex = GetStartIndexFromKey(key);
            int endIndex = startIndex + _numEntry - 1;
            VData ans = default(VData);

            // need a readLock
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (_cacheArray[i] == default(TItem) || _cacheArray[i] == null || _cacheArray[i].IsEmpty)
                    { continue; }

                    if (((IEquatable<UKey>)_cacheArray[i].Key).Equals(key))
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

        public void Put(UKey key, VData data)
        {
            // if disposed, throw exception
            EnsureNotDisposed();
            
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

        protected int GetKeyHash(UKey key)
        {
            return key.GetHashCode();
        }

        protected int GetStartIndexFromKey(UKey key)
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
                    new ReadOnlyCollection<TItem>(
                        (IList<TItem>)new ArraySegment<TItem>(_cacheArray, startIndex, endIndex - startIndex + 1)));

                // Make sure replacement strategy didn't do something crazy
                if (ans < 0 || ans > (endIndex - startIndex))
                { throw new ArgumentException(string.Format("IReplacementAlgorithm ({0}) returned invalid index value.", _replacementFinder.GetType().FullName), "replacementAlgo"); }

                // determine index in full cache
                return startIndex + ans;
            }
            finally
            { cacheLock.ExitReadLock(); }
        }

        protected int GetFirstEmptyIndex(int startIndex, int endIndex)
        {
            // theoretically we should already be ReadLocked at this point,
            // but for safety, we should check it
            bool isOurReadLock = false;
            if (!cacheLock.IsReadLockHeld)
            {
                cacheLock.EnterReadLock();
                isOurReadLock = true;
            }
            try
            {
                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (_cacheArray[i] == null || _cacheArray[i].IsEmpty)
                    { return i; }
                }
                return -1;
            }
            finally 
            { 
                if (isOurReadLock)
                    cacheLock.ExitReadLock(); 
            }
        }
    }
}