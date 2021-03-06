using System;
using Xunit;
using Xunit.Abstractions;
using associative_cache;
using associative_cache.ReplacementAlgorithm;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using associative_cache.Interfaces;
using associative_cache.ReplacementAlgorithm.Interfaces;
using System.Collections.ObjectModel;

namespace associative_cache_test
{
    public class UnitTest1
    {
        private readonly ITestOutputHelper _output;

        public UnitTest1(ITestOutputHelper output)
        {
            this._output = output;
        }

        private ICache<int, string> _leastCache =
            new Cache<AccessTrackedCacheEntry<int, string>, int, string>(2, 4, new LeastRecentlyUsed<AccessTrackedCacheEntry<int, string>, int, string>());
        private ICache<int, string> _mostCache =
            new Cache<AccessTrackedCacheEntry<int, string>, int, string>(2, 4, new MostRecentlyUsed<AccessTrackedCacheEntry<int, string>, int, string>());

        private ICache<int, string> _firstCache = 
            new Cache<CacheEntry<int, string>, int, string>(2, 4, new AlwaysFirstSetReplacement<CacheEntry<int, string>, int, string>());

        private List<Tuple<int, string>> cacheables = new List<Tuple<int, string>>(new Tuple<int, string>[] {
            new Tuple<int, string>(1, "one"),
            new Tuple<int, string>(2, "two"),
            new Tuple<int, string>(3, "three"),
            new Tuple<int, string>(4, "four"),
            new Tuple<int, string>(5, "five"),
            new Tuple<int, string>(6, "six"),
            new Tuple<int, string>(7, "seven"),
            new Tuple<int, string>(8, "eight")
        });

        private List<Tuple<int, string>> extraCacheables = new List<Tuple<int, string>>(new Tuple<int, string>[] {
            new Tuple<int, string>(9, "nine"),
            new Tuple<int, string>(10, "ten"),
            new Tuple<int, string>(11, "eleven"),
            new Tuple<int, string>(12, "twelve"),
            new Tuple<int, string>(13, "thirteen"),
            new Tuple<int, string>(14, "fourteen"),
            new Tuple<int, string>(15, "fifteen"),
            new Tuple<int, string>(16, "sixteen")
        });

        [Fact]
        public void TestAddOne()
        {
            _leastCache.Put(cacheables[0].Item1, cacheables[0].Item2);
            Assert.Equal(cacheables[0].Item2, _leastCache.Get(cacheables[0].Item1));
        }

        [Fact]
        public async Task TestAddAll()
        {
            AddAllCacheables(_leastCache);

            var results = await Task.WhenAll(cacheables.Select(async item =>
            {
                var i = await Task.Run(() => _leastCache.Get(item.Item1));
                return new Tuple<string, string>(item.Item2, i);
            }));

            Assert.All(results, result => Assert.Equal(result.Item1, result.Item2));
        }

        [Fact]
        public void TestOneLeastReplacement()
        {
            AddAllCacheables(_leastCache);

            // now we add one more, which should be added 
            // to set 2 overwriting the oldest one, which
            // should be [1]="one"
            _leastCache.Put(extraCacheables[0].Item1, extraCacheables[0].Item2);

            // Verify it's there
            Assert.Equal(extraCacheables[0].Item2, _leastCache.Get(extraCacheables[0].Item1));

            // Verify all the originals are still there, save for [1]="one"
            foreach (Tuple<int, string> item in cacheables)
            {
                if (item.Item1 == 1)
                { Assert.NotEqual(item.Item2, _leastCache.Get(item.Item1)); }
                else
                { Assert.Equal(item.Item2, _leastCache.Get(item.Item1)); }
            }
        }

        [Fact]
        public void TestOneMostReplacement()
        {
            AddAllCacheables(_mostCache);

            // now we add one more, which should be added 
            // to set 2 overwriting the newest one, which
            // should be [7]="seven"
            _mostCache.Put(extraCacheables[0].Item1, extraCacheables[0].Item2);

            // Verify it's there
            Assert.Equal(extraCacheables[0].Item2, _mostCache.Get(extraCacheables[0].Item1));

            // Verify all the originals are still there, save for [7]="seven"
            foreach (Tuple<int, string> item in cacheables)
            {
                if (item.Item1 == 7)
                { Assert.NotEqual(item.Item2, _mostCache.Get(item.Item1)); }
                else
                { Assert.Equal(item.Item2, _mostCache.Get(item.Item1)); }
            }
        }

        [Fact]
        public void TestMemoryUsage()
        {
            long startMem = 0,
                endMem = 0;

            GC.Collect(); // start clean
            _output.WriteLine("Initial Memory: {0:###,###,###,###,##0} bytes", startMem = GC.GetTotalMemory(false));

            AddAllCacheables(_leastCache);

            _output.WriteLine("Full cache Memory: {0:###,###,###,###,##0} bytes", GC.GetTotalMemory(false));
            
            _leastCache.Put(extraCacheables[0].Item1, extraCacheables[0].Item2);

            _output.WriteLine("Full +1 cache Memory: {0:###,###,###,###,##0} bytes", GC.GetTotalMemory(false));

            _leastCache.Clear();

            _output.WriteLine("Cleared (before GC) cache Memory: {0:###,###,###,###,##0} bytes", GC.GetTotalMemory(false));
            GC.Collect();
            _output.WriteLine("Cleared (post GC) cache Memory: {0:###,###,###,###,##0} bytes", endMem = GC.GetTotalMemory(true));

            Assert.True(endMem <= startMem);
        }

        [Fact]
        public void TestCustomReplacementAlgorithm()
        {
            AddAllCacheables(_firstCache);

            // now we add one more, which should be added 
            // to set 2 overwriting the first one, which
            // should be [1]="one"
            _firstCache.Put(extraCacheables[0].Item1, extraCacheables[0].Item2);

            // Verify it's there
            Assert.Equal(extraCacheables[0].Item2, _firstCache.Get(extraCacheables[0].Item1));

            // Verify all the originals are still there, save for [1]="one"
            foreach (Tuple<int, string> item in cacheables)
            {
                if (item.Item1 == 1)
                { Assert.NotEqual(item.Item2, _firstCache.Get(item.Item1)); }
                else
                { Assert.Equal(item.Item2, _firstCache.Get(item.Item1)); }
            }
        }

        [Fact]
        public void TestMultithreadedCache()
        {
            // Create an arbitrary number of threads and run them in parallel
            int numThreads = 100000, // 100,000 should take a little under 2 seconds to test, on a decent CPU
                milSecsPerThread = 15; // timer accuracy is around 15ms, lower values likely have no impact
            TaskFactory factory = new TaskFactory();

            // We'll just add things to the cache, since writes should have
            // the largest possibility of deadlocks, since reads can occur in parallel
            // We can't really test what is added when, because we can't control the thread timings
            Task[] tasks = Enumerable.Range(1,numThreads).Select(item =>
            {
                return factory.StartNew(() => { AddAllCacheables(_leastCache); });
            }).ToArray();

            // Success is based on whether all tasks complete within the given timeout
            Assert.True(Task.WaitAll(tasks, numThreads * milSecsPerThread), "Threads did not execute in given time. Possible deadlock.");
        }

        private void AddAllCacheables(ICache<int, string> cache)
        {
            foreach (Tuple<int, string> item in cacheables)
            {
                cache.Put(item.Item1, item.Item2);
            }
        }
    }

    public class AlwaysFirstSetReplacement<T, U, V> : IReplacementAlgorithm<T, U, V>
        where T : CacheEntry<U, V>
    {
        public int GetReplacementIndex(ReadOnlyCollection<T> set)
        {
            // we just always return the first element of the set
            return 0;
        }
    }
}
