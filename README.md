# associative_cache

## Purpose

To create an N-way, set-associative cache library in .NET that is thread-safe and as performant as possible. Additional goals including ensuring that the cache is flexible enough to store any type of keys and data while maintaining type-safety, providing a composable way to supply new replacement algorithms and keeping the memory footprint as tight as possible.

N-way, set-associative caches attempt to balance the pros and cons between a direct-mapped cache and a fully-associative cache. In a direct-mapped cache, reads and writes are fast, but because each key has only one block that it can be stored in, there can be wasted cache space and increased misses. A fully-associative cache, on the other hand, allows a key to be stored anywhere in the cache and makes good use of the cache space, but reads can be extremely slow due to the linear lookup that needs to be performed on read operations. In a set-associative cache, the cache is split into sets and each key will be assigned a specific set that it can be written to or read from. This reduces the lookup time for read operations but still makes decent use of the full cache, reducing the number of misses.

When a write operation is performed on a cache and the set assigned to the key is full, an eviction of obsolete data must be performed for the data to be cached. There are numerous algorithms that can be used to determine what data is deemed obsolete and, thus, evicted. The most common are the Least Recently Used (LRU) and Most Recently Used (MRU) algorithms. These will be implemented by the library described in this design document. While these algorithms may be among the most common, there are specific applications that may require the use of different algorithms, so the library will also provide a way for consumers to develop their own algorithms for determining the evicted data.

## Design

There are three main components to this cache library. The CacheEntry object - essentially a generic class used to define the key and value type, and facilitate storing a block of data in the cache. The second component is the Cache object. This is the actual cache and contains an array of the CacheEntry objects. The third main component of the library is the replacement algorithm - an object that implements the IReplacementAlgorithm interface. 

The CacheEntry object is a generic class that requires a type for the key and value of the data being cached. Along with Key and Data properties used to access this information, the CacheEntry also provides an IsEmpty property which defines if that block in the Cache is empty, despite the value of the Data property. This allows clearing a block quickly and is memory efficient. It will be explained in detail in the description of the Cache object. While the CacheEntry provides most of the properties needed to store and retrieve cached data, it does not do much in the way of determining what data should be replaced. The goal is to make CacheEntry slim and allow it to be a base class for inheriting classes to build upon. These classes can then add properties used to track what data should be replaced. One example of this in the library is the AccessTrackedCacheEntry object. This class adds Timestamp and AccessCount properties along with implementing the IOnAccessCacheEntry interface.  This allows for the AccessTrackedCacheEntry to update the Timestamp and AccessCount each time the cache block is read or written to in order to track how recently and how often it has been used in the cache.

The Cache object is also a generic class, which the generic types define the CacheEntry object type as well as the Key and Data types. The main contributions of the Cache object are the constructor, the Get() method, and the Put() method. There is a method that allows the consumer to check the Cache object's size as well as a Clear() method to clear the cache. This class is disposable, ensuring the private ReaderWriterLockSlim object (used to ensure thread safety) is properly cleaned up.  The Cache object's constructor is used to create a cache while defining its size and replacement algorithm.  The Put() method will store a given key/value pair in the cache and the Get() method will attempt to retrieve the associated value given a key.

The replacement algorithm is defined by a class that implements the IReplacementAlgorithm interface.  The implementations of the LRU and MRU replacement algorithms provided by the library implement this interface. The interface defines a single method, GetReplacementIndex(), that will return the index of the evicted cache block - given a slice of the cache array as a ReadOnlyCollection<T>, where T is the specific CacheEntry object stored in the cache. Because the list of items passed to this method is strongly typed, the LRU and MRU algorithm implementations can access the Timestamp and AccessCount properties of the cached items to be used in their logic of determining which block to evict. Furthermore, the LRU and MRU implementations define that the CacheEntry objects used must be of type AccessTrackedCacheEntry where these properties are defined.  It forces the consumer to use expected CacheEntry types with specific IRepalcementAlgorithm types.

## Design Choice Reasonings

Generics were chosen when designing the classes in the library to provide flexibility and extensibility while maintaining type-safety.  Once a Cache object has been instantiated, the types are defined and the methods will always return an expected type.  Using generic types should also reduce, or remove, the consumer’s need to box, or unbox, values going into, or coming out of, the cache.

The CacheEntry object was designed simplistically with extension interfaces for tracking metadata, such as access.  This was done to ensure the consumer can expand the CacheEntry type for their own replacement algorithm needs while paying the memory cost of only the data and metadata needed to store.

Providing the CacheEntry object with an IsEmpty property allows a cache block to be deleted or initialized without creating a new object instance and maintains the memory footprint of the cache. If the cache is being used, the consumer will know how much memory they can use (and want to use) for the cache. The library needs to respect that value without going significantly above it at any time, as this could result in out of memory exceptions. In normal applications to replace an object in an array, it might be common to set the index of the array equal to a new instance of that object. In .NET (a garbage collection language), the old object value will remain in memory until the garbage is collected. In an application that is under heavy load, this could happen often enough that this behavior could cause the cache to take up significantly more memory than the consumer had planned.  By simply setting the IsEmpty property to true for an object in the cache, the code can determine still that it has no data of value but the size of the cache remains constant. This methodology is faster, since changing the value of a boolean would take less time than allocating memory and creating a whole new object instance.

Another design choice made to ensure a consistent memory footprint of the cache is to send a ReadOnlyCollection<T> wrapper of an ArraySegment<T> object to the IReplacementAlgorithm.GetReplacementIndex() method. The ArraySegment class allows a slice of an array to be referenced without making a copy of the objects in that slice. This allows us to send the cache set in question to the GetReplacementIndex() method without allocating the memory needed to copy that set, but still provides the GetReplacementIndex() method full access to the strongly-type CacheEntry objects needed to make its eviction determination. Wrapping this ArraySegment object in a ReadOnlyCollection class prevents the GetReplacementIndex() method from altering the cache and, again, does not significantly changes the memory footprint of the cache.

While the cache array could have been implemented as a two-dimensional array, a one-dimensional array was used because access times are faster in one-dimensional arrays. Because of this choice the following algorithm is used to determine the starting index of a set for a given key: Idx = (Kh * Ns) * Ne, where Idx is the starting index, Kh is the hash code of the key, Ns is the number of sets in the cache and Ne is the number of entries per set.

The ReaderWriterLockSlim was chosen to implement thread safety because it allows parallel reads and because it’s faster than a ReaderWriterLock class. Read locks are used whenever the cache is being read, but since these reads can happen in multiple threads simultaneously, they only block when a write lock is started. In the case of a cache hit during the Get() method, if the CacheEntry object implements the IOnAccessCacheEntry interface, then the read lock is upgraded to a write lock to ensure the access to the cache block is updated without concurrency issues.  The only other processes that enable a write lock are the Put() and Clear() methods. This should allow many threads to run simultaneously without being blocked.

## Using and Extending the Library

The basic usage is to first create an instance of the Cache object and then call a series of Put() and Get() methods to store and retrieve data. Part of this is determining what type of CacheEntry, key, data, and replacement algorithm types you plan to use. To create a cache that has 2 sets of 4 entries each and stores string values with integer keys using the LRU replacement algorithm use the following:

    ICache<int, string> cache =
            new Cache<AccessTrackedCacheEntry<int, string>, int, string>(2, 4, new LeastRecentlyUsed<AccessTrackedCacheEntry<int, string>, int, string>());

The variable type should be defined using the ICache<TKey, UData> simply because it is easier to pass around through the application as opposed to the specific type (in this case: Cache<AccessTrackedCacheEntry<int, string>, int, string>).  Once the cache is created a new item can be added using:

    cache.Put(1, “new item”);

And retrieving items can be done using:

    string response = cache.Get(1);

To clear the cache and start over use:

    cache.Clear();

Once the cache is no longer needed the Dispose() method should be called. This can be done directly or by wrapping the cache object in a using statement.

Extending the cache library will likely be done primarily to implement a new replacement algorithm. To demonstrate how to do this, the LRU implementation will be described. In order to track which item was used the least recently, the CacheEntry object used in the cache will need to track when the object was last accessed and how many times. Consider the following code:

    public interface IAccessCountedCacheEntry : 
        IOnAccessCacheEntry
    {
        int AccessCount { get; }
    }

    public interface IAccessTimestampedCacheEntry : 
        IOnAccessCacheEntry
    {
        DateTime Timestamp { get; }
    }

    public class AccessTrackedCacheEntry<T, U> : 
        CacheEntry<T, U>, IAccessCountedCacheEntry, 
        IAccessTimestampedCacheEntry, IOnAccessCacheEntry
    {
        // start at one since creation is considered access
        public int AccessCount { get; set; } = 1; 

        public DateTime Timestamp { get; protected set; } = 
        DateTime.MinValue.ToUniversalTime();

        public void OnDataAccess()
        {
            OnDataAccess(false);
        }

        public void OnDataAccess(bool newValues)
        {
            // if not updating just increment count
            if (!newValues)
            {  AccessCount++; }
            else
            {
                // otherwise we need to reset the access count
                // because this is a 'new' cached object
                AccessCount = 1;
            }
            // always update the timestamp
            Timestamp = DateTime.UtcNow;
        }
    }

First two interfaces are defined to create new property contracts for the CacheEntry object. These are not entirely necessary, but it was defined this way in the library so they could be used by the consumer, if desired. The AccessTrackedCacheEntry object they implements these interfaces and also the IOnAccessCacheEntry interface.  This interface defines two methods: 

    void OnDataAccess();

    void OnDataAccess(bool newValues);

These methods are called by the cache whenever the object is accessed, either by a read hit or a write. The OnDataAccess(bool newValues) method will be passed a true value for newValues if the cache determines that this write operation is overwriting an evicted data block or is writing to an empty data block. So, in the case of this AccessTrackedCacheEntry object, the AccessCount property is incremented on read hits or reset to 1 on writes. The Timestamp property is always updated to the current UTC time.

This creates a new CacheEntry object that tracks when data is being accessed. Creating a new CacheEntry object is not always needed for a new replacement algorithm implementation, but the following will always be required. In order to implement the LRU replacement algorithm, consider the following code:

    public class LeastRecentlyUsed<T, U, V> : 
        IReplacementAlgorithm<T, U, V>
        where T : AccessTrackedCacheEntry<U, V>
    {
        public int GetReplacementIndex(ReadOnlyCollection<T> set)
        {
            int ans = -1,
            min = Int32.MaxValue;
            DateTime minTime = DateTime.MaxValue.ToUniversalTime();
            for (int i = 0; i < set.Count; i++)
            {
                if (set[i].AccessCount < min ||
                    (set[i].AccessCount == min && set[i].Timestamp < minTime))
                {
                    ans = i;
                    min = set[i].AccessCount;
                    minTime = set[i].Timestamp;
                }
            }

            return ans;
        }
    }

The keys to notice are that this class implements the IReplacementAlgorithm<T, U, V> interface and the maintains the <T, U, V> generics in its own definition. This allows the generics flexibility to extend to this class. Also notice that, in this case, the class defines that T (the type of CacheEntry object used) must be of type AccessTrackedCacheEntry object. This allows the GetReplacementIndex() method to access the AccessCount and Timestamp properties on the objects in the cache. Those are the basics to creating a new replacement algorithm. The GetReplacementIndex() method should return a number between 0 and set.Count - 1 to indicate which object in that set should be evicted and replaced.

