using Game.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Tests the keyed get-or-add reuse semantics of <see cref="RedisMultiplexerFactory"/> (#696). The connect
    /// step itself opens a real Redis connection, so it is covered by integration tests rather than here; this
    /// exercises the cache/lock logic through the generic <c>GetOrAdd</c> seam with sentinel values and a stub
    /// factory, which is exactly the branch that decides whether two requests share a multiplexer or each open
    /// their own.
    /// </summary>
    public class RedisMultiplexerFactoryTests
    {
        [Fact]
        public void GetOrAdd_SameKey_ReturnsSameValueAndCreatesOnce()
        {
            var cache = new Dictionary<string, object>();
            var syncRoot = new object();
            var created = 0;
            object Factory(string _)
            {
                created++;
                return new object();
            }

            var first = RedisMultiplexerFactory.GetOrAdd(cache, syncRoot, "localhost:6379", Factory);
            var second = RedisMultiplexerFactory.GetOrAdd(cache, syncRoot, "localhost:6379", Factory);

            // Same key reuses the cached instance and never invokes the factory again.
            Assert.Same(first, second);
            Assert.Equal(1, created);
        }

        [Fact]
        public void GetOrAdd_DifferentKeys_ReturnsDistinctValues()
        {
            var cache = new Dictionary<string, object>();
            var syncRoot = new object();
            object Factory(string _) => new object();

            var cacheValue = RedisMultiplexerFactory.GetOrAdd(cache, syncRoot, "cache:6379", Factory);
            var pubSubValue = RedisMultiplexerFactory.GetOrAdd(cache, syncRoot, "pubsub:6380", Factory);

            // Distinct connection strings get distinct instances; identical ones (above) would share.
            Assert.NotSame(cacheValue, pubSubValue);
        }

        [Fact]
        public void GetOrAdd_KeysByExactString_DoesNotNormalize()
        {
            var cache = new Dictionary<string, object>();
            var syncRoot = new object();
            object Factory(string _) => new object();

            // Two strings that are semantically equivalent but not byte-identical are treated as different keys —
            // the deliberate trade-off of keying by the raw string rather than a normalized configuration.
            var lower = RedisMultiplexerFactory.GetOrAdd(cache, syncRoot, "localhost:6379", Factory);
            var upper = RedisMultiplexerFactory.GetOrAdd(cache, syncRoot, "LOCALHOST:6379", Factory);

            Assert.NotSame(lower, upper);
        }

        [Fact]
        public void GetOrAdd_ConcurrentFirstRequests_ShareOneCreatedValue()
        {
            var cache = new Dictionary<string, object>();
            var syncRoot = new object();
            var created = 0;
            object Factory(string _)
            {
                Interlocked.Increment(ref created);
                return new object();
            }

            // Many threads racing on the same key must collapse onto a single created instance — the startup race
            // the locked get-or-add closes. Dedicated threads (not the thread pool) start together on a barrier so
            // the race is genuine and so the test does not starve the pool the BackgroundWorker tests share.
            const int threadCount = 16;
            var results = new object[threadCount];
            using var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];

            for (var i = 0; i < threadCount; i++)
            {
                var index = i;
                threads[index] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    results[index] = RedisMultiplexerFactory.GetOrAdd(cache, syncRoot, "localhost:6379", Factory);
                });
                threads[index].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.Equal(1, created);
            Assert.All(results, value => Assert.Same(results[0], value));
        }

        [Fact]
        public async Task DisposeAllAsync_DisposesEveryEntry_AndClearsTheCache()
        {
            var cache = new Dictionary<string, FakeAsyncDisposable>();
            var syncRoot = new object();
            var first = new FakeAsyncDisposable();
            var second = new FakeAsyncDisposable();
            cache["cache:6379"] = first;
            cache["pubsub:6380"] = second;

            await RedisMultiplexerFactory.DisposeAllAsync(cache, syncRoot, NullLogger.Instance);

            // Every cached connection is disposed and the cache is emptied so a later request reconnects fresh —
            // the graceful-shutdown teardown the hosted service drives.
            Assert.Equal(1, first.DisposeCount);
            Assert.Equal(1, second.DisposeCount);
            Assert.Empty(cache);
        }

        [Fact]
        public async Task DisposeAllAsync_EmptyCache_IsANoOp()
        {
            var cache = new Dictionary<string, FakeAsyncDisposable>();

            await RedisMultiplexerFactory.DisposeAllAsync(cache, new object(), NullLogger.Instance);

            Assert.Empty(cache);
        }

        [Fact]
        public async Task DisposeAllAsync_OneEntryFaultsOnDispose_StillDisposesTheRestAndClearsTheCache()
        {
            var cache = new Dictionary<string, FakeAsyncDisposable>();
            var syncRoot = new object();
            var faulting = new FakeAsyncDisposable(throwOnDispose: true);
            var healthy = new FakeAsyncDisposable();
            cache["cache:6379"] = faulting;
            cache["pubsub:6380"] = healthy;
            var logger = new CapturingLogger();

            // The contract documented on DisposeAllAsync: one faulting close must not abort the rest of the drain,
            // and must not throw out of the caller (the host's graceful-shutdown hook).
            await RedisMultiplexerFactory.DisposeAllAsync(cache, syncRoot, logger);

            Assert.Equal(1, faulting.DisposeCount);
            Assert.Equal(1, healthy.DisposeCount);
            Assert.Empty(cache);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.NotNull(entry.Exception);
        }

        private sealed class FakeAsyncDisposable(bool throwOnDispose = false) : IAsyncDisposable
        {
            public int DisposeCount { get; private set; }

            public ValueTask DisposeAsync()
            {
                DisposeCount++;
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("redis close wedged");
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
