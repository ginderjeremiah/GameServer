using Game.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Tests the keyed get-or-connect reuse semantics of <see cref="RedisMultiplexerFactory"/> (#696, #2371). The
    /// connect step itself opens a real Redis connection, so it is covered by integration tests rather than here;
    /// this exercises the cache/lock/negative-cache logic through the generic <c>GetOrConnect</c> seam with
    /// sentinel values and a stub factory, which is exactly the branch that decides whether resolvers share a
    /// multiplexer, race a first connect, or fail fast during an outage.
    /// </summary>
    public class RedisMultiplexerFactoryTests
    {
        private static readonly Action<object> NoOpDiscard = _ => { };

        [Fact]
        public void GetOrConnect_SameKey_ReturnsSameValueAndCreatesOnce()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            var created = 0;
            object Factory(string _)
            {
                created++;
                return new object();
            }

            var first = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => DateTime.UtcNow);
            var second = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => DateTime.UtcNow);

            // Same key reuses the cached instance and never invokes the factory again once one is published.
            Assert.Same(first, second);
            Assert.Equal(1, created);
        }

        [Fact]
        public void GetOrConnect_DifferentKeys_ReturnsDistinctValues()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            object Factory(string _) => new object();

            var cacheValue = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "cache:6379", Factory, NoOpDiscard, NullLogger.Instance, () => DateTime.UtcNow);
            var pubSubValue = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "pubsub:6380", Factory, NoOpDiscard, NullLogger.Instance, () => DateTime.UtcNow);

            // Distinct connection strings get distinct instances; identical ones (above) would share.
            Assert.NotSame(cacheValue, pubSubValue);
        }

        [Fact]
        public void GetOrConnect_KeysByExactString_DoesNotNormalize()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            object Factory(string _) => new object();

            // Two strings that are semantically equivalent but not byte-identical are treated as different keys —
            // the deliberate trade-off of keying by the raw string rather than a normalized configuration.
            var lower = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => DateTime.UtcNow);
            var upper = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "LOCALHOST:6379", Factory, NoOpDiscard, NullLogger.Instance, () => DateTime.UtcNow);

            Assert.NotSame(lower, upper);
        }

        [Fact]
        public void GetOrConnect_ConcurrentFirstRequests_PublishOneWinner_AndDiscardEveryLoser()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            var createdValues = new List<object>();
            var createdGate = new object();
            object Factory(string _)
            {
                var value = new object();
                lock (createdGate)
                {
                    createdValues.Add(value);
                }

                return value;
            }

            var discarded = new List<object>();
            var discardGate = new object();
            void Discard(object value)
            {
                lock (discardGate)
                {
                    discarded.Add(value);
                }
            }

            // Many threads racing on the same key no longer serialize behind one connect (#2371): each may create
            // its own value, but only one is ever published and every other created value is discarded exactly
            // once, so callers still converge on a single shared instance.
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
                    results[index] = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, Discard, NullLogger.Instance, () => DateTime.UtcNow);
                });
                threads[index].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            var winner = results[0];
            Assert.All(results, value => Assert.Same(winner, value));
            Assert.Contains(winner, createdValues);

            // Every created value except the published winner was discarded, and none twice.
            var expectedDiscarded = createdValues.Where(value => !ReferenceEquals(value, winner)).ToList();
            Assert.Equal(expectedDiscarded.Count, discarded.Count);
            Assert.All(expectedDiscarded, value => Assert.Contains(value, discarded));
            Assert.All(discarded, value => Assert.Contains(value, expectedDiscarded));
        }

        [Fact]
        public void GetOrConnect_FactoryThrows_CachesFailureAndFailsFastWithoutRetryingWithinBackoff()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            var attempts = 0;
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            object Factory(string _)
            {
                attempts++;
                throw new InvalidOperationException("redis unreachable");
            }

            var first = Assert.Throws<InvalidOperationException>(() =>
                RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => now));

            // A second resolver arriving moments later must not pay for another ~5s connect attempt — it gets the
            // same remembered failure back immediately instead.
            var second = Assert.Throws<InvalidOperationException>(() =>
                RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => now));

            Assert.Equal(1, attempts);
            Assert.Same(first, second);
        }

        [Fact]
        public void GetOrConnect_FactoryThrows_RetriesOnceBackoffElapses()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            var attempts = 0;
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            object Factory(string _)
            {
                attempts++;
                throw new InvalidOperationException($"redis unreachable, attempt {attempts}");
            }

            Assert.Throws<InvalidOperationException>(() =>
                RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => now));

            // Once the backoff window has fully elapsed, the next resolver is allowed to retry rather than being
            // fast-failed forever.
            var later = now.AddSeconds(10);
            Assert.Throws<InvalidOperationException>(() =>
                RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => later));

            Assert.Equal(2, attempts);
        }

        [Fact]
        public void GetOrConnect_SucceedsAfterAPriorFailure_ClearsTheRememberedFailure()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            var attempts = 0;
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            object Factory(string _)
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("redis unreachable");
                }

                return new object();
            }

            Assert.Throws<InvalidOperationException>(() =>
                RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => now));

            var later = now.AddSeconds(10);
            var value = RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, NullLogger.Instance, () => later);

            Assert.NotNull(value);
            Assert.Empty(failures);
            Assert.Same(value, cache["localhost:6379"]);
        }

        [Fact]
        public void GetOrConnect_FactoryThrows_LogsAWarning()
        {
            var cache = new Dictionary<string, object>();
            var failures = new Dictionary<string, RedisMultiplexerFactory.ConnectFailure>();
            var syncRoot = new object();
            var logger = new CapturingLogger();
            object Factory(string _) => throw new InvalidOperationException("redis unreachable");

            Assert.Throws<InvalidOperationException>(() =>
                RedisMultiplexerFactory.GetOrConnect(cache, failures, syncRoot, "localhost:6379", Factory, NoOpDiscard, logger, () => DateTime.UtcNow));

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.IsType<InvalidOperationException>(entry.Exception);
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
