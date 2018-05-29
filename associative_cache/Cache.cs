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
    /// <summary>
    /// Defines an N-way, set-associative cache object for storing <c>CacheEntry</c> objects 
    /// </summary>
    /// <typeparam name="TItem">The type of <see cref="CacheEntry{UKey,VData}"/> objects this cache will store and retrieve</typeparam>
    /// <typeparam name="UKey">The type of keys used in the <see cref="CacheEntry{UKey,VData}"/> objects</typeparam>
    /// <typeparam name="VData">The type of data used in the <see cref="CacheEntry{UKey,VData}"/> objects</typeparam>
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

        /// <summary>
        /// Creates a new instance of the <c>Cache</c> object using the provided number of sets, number of entries and replacement algorithm
        /// </summary>
        /// <param name="numSet">The number of sets the <see cref="Cache{TItem, UKey, VData}"/> will have</param>
        /// <param name="numEntry">The number of entries in each set</param>
        /// <param name="replacementAlgo">The type of <see cref="IReplacementAlgorithm{TItem, UKey, VData}"/> object that will be used to determine objects that will be replaced when a set is full during a <see cref="Cache{TItem, UKey, VData}.Put(UKey, VData)"/> operation</param>
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

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="Cache{TItem, UKey, VData}"/> and optionally releases the managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unamanged resources</param>
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

        /// <summary>
        /// Releases all resources used by the <see cref="Cache{TItem, UKey, VData}"/>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the total size of the <see cref="Cache{TItem, UKey, VData}"/>
        /// </summary>
        /// <returns>The total size of the <see cref="Cache{TItem, UKey, VData}"/>, <c>numberOfSets * numberOfEntries</c> as an <c>int</c></returns>
        public int Size
        {
            get { return _numEntry * _numSet; }
        }

        /// <summary>
        /// Ensures all objects in the <see cref="Cache{TItem, UKey, VData}"/> are marked as empty so they can be reused
        /// </summary>
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

        /// <summary>
        /// Uses the provided key to lookup data in the <see cref="Cache{TItem, UKey, VData}"/> and returns it
        /// </summary>
        /// <param name="key">The key used to lookup data in the <see cref="Cache{TItem, UKey, VData}"/></param>
        /// <returns>If the key is found, returns the data associated with it, otherwise <c>default(U)</c></returns>
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

        /// <summary>
        /// Stores the provided data in the <see cref="Cache{TItem, UKey, VData}"/> using the key to determine its destination set
        /// </summary>
        /// <param name="key">The key used to store the data in the <see cref="Cache{TItem, UKey, VData}"/>, determines the data's set as well as association for lookups using <see cref="Cache{TItem, UKey, VData}.Get(UKey)"/></param>
        /// <param name="data">The data to be stored in the <see cref="Cache{TItem, UKey, VData}"/></param>
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

        /// <summary>
        /// If the <see cref="Cache{TItem, UKey, VData}"/> has been disposed, throws an <see cref="ObjectDisposedException" />; otherwise does nothing
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the <see cref="Cache{TItem, UKey, VData}"/> has been disposed already</exception>
        protected void EnsureNotDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("This object has been disposed and cannot be used.");
        }

        /// <summary>
        /// Returns the hash code of a given key
        /// </summary>
        /// <param name="key">The key to be hashed</param>
        /// <returns>The hash code of the given key</returns>
        protected int GetKeyHash(UKey key)
        {
            return key.GetHashCode();
        }

        /// <summary>
        /// Returns the starting index within the <see cref="Cache{TItem, UKey, VData}"/> of the set that the given key belongs to
        /// </summary>
        /// <param name="key">The key whose set's starting index should be found</param>
        /// <returns>The starting index of the set that <c>key</c> belongs to</returns>
        protected int GetStartIndexFromKey(UKey key)
        {
            return (GetKeyHash(key) % _numSet) * _numEntry;
        }

        /// <summary>
        /// Returns the index of the entry to replace in a given set of the <see cref="Cache{TItem, UKey, VData}"/>; this could be an empty entry or an entry that should be replaced
        /// </summary>
        /// <param name="startIndex">The starting index of the set</param>
        /// <param name="endIndex">The ending index of the set</param>
        /// <returns>An index inclusively between <c>startIndex</c> and <c>endIndex</c> that should be replaced</returns>
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

        /// <summary>
        /// Iterates through a set of the <see cref="Cache{TItem, UKey, VData}"/> and returns the index of the first empty entry, if any
        /// </summary>
        /// <param name="startIndex">The starting index of the set</param>
        /// <param name="endIndex">The ending index of the set</param>
        /// <returns>If an empty entry is found, its index is returned, otherwise -1</returns>
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