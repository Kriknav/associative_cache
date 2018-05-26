/*
    Goals:
        - Create a base CacheEntry object that can be used to store arbitrary objects in the cache
        - Make this object extensible; this way consumers only pay for what they need

    The base object should support the basics of a tag and data, but extensions can be created
    to track other things like access count, last-accessed timestamps and other data needed when 
    calculating the replacement index. Here we will create the base type and inherited types to deal 
    with LRU and MRU replacement algorithms
 */

using System.Collections;
using System.Collections.Generic;
using associative_cache.Interfaces;

namespace associative_cache
{
    /// <summary>
    /// A basic <c>CacheEntry</c> object type for storing arbitrary data in a memory cache
    /// </summary>
    /// <typeparam name="T">The type of keys to use, must implement <see cref="IEqualityComparer" /></typeparam>
    /// <typeparam name="U">The type of data to store</typeparam>
    public class CacheEntry<T, U> : ICacheable
        //where T : IEqualityComparer<T> // Let's make sure that our keys are hashable
    {
        /// <summary>
        /// Private backing member for <c>CacheEntry</c> data value.
        /// </summary>
        private U _data;

        /// <value>
        /// Gets or sets the data value for the <c>CacheEntry</c> object
        /// </value>
        public U Data
        {
            get { return _data; }
            set
            {
                _data = value;
                IsEmpty = _data.Equals(default(U)); // Maintain the emptiness of the entry
            }
        }
        /// <value>
        /// Gets or sets the key value for the <c>CacheEntry</c> object.
        /// </value>
        public T Key { get; set; }

        /// <value>
        /// Gets the value indicating whether the <c>CacheEntry</c> object's data is empty.
        /// </value>
        public bool IsEmpty { get; internal set; }

        /// <summary>
        /// Creates an empty <c>CacheEntry</c> object
        /// </summary>
        public CacheEntry()
            : this(default(T), default(U), true)
        { }

        /// <summary>
        /// Creates a new <c>CacheEntry</c> and intializes it's key and data values. 
        /// </summary>
        /// <param name="key">The key value to use, must be an IEqualityComparer <typeparamref name="T"/> type </param>
        /// <param name="data">The data value to use</param>
        public CacheEntry(T key, U data)
            : this(key, data, false)
        { }

        /// <summary>
        /// Creates a new <c>CacheEntry</c> with the specified initial values.
        /// </summary>
        /// <param name="key">The key value to use, must be an IEqualityComparer <typeparamref name="T"/> type</param>
        /// <param name="data">The data value to use</param>
        /// <param name="isEmpty">Determines if the <c>CacheEntry</c> object is initially marked as empty or not</param>
        private CacheEntry(T key, U data, bool isEmpty)
        {
            Key = key;
            Data = data;
            IsEmpty = isEmpty;
        }
    }
    
}