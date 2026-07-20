using Game.Abstractions.Infrastructure;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Covers the Redis-backed <see cref="IPubSubQueue"/> (RedisQueue) overloads that the write-behind path leans on
    /// but that the <see cref="Game.DataAccess.DataProviderSynchronizer"/> tests don't reach: the typed
    /// serialize/deserialize round-trips and popping past the end of an empty queue. RedisQueue is a thin adapter over
    /// an out-of-process dependency, so per the testing guidelines it is exercised through an integration test rather
    /// than mocked. Each test uses a unique queue name so it is independent of any residual queue state.
    /// </summary>
    [Collection("Integration")]
    public class RedisQueueIntegrationTests : ApplicationIntegrationTestBase
    {
        public RedisQueueIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task TypedRoundTripAsync_SerializesAndDeserializes_ThenReturnsNullWhenDrained()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            var payload = new SamplePayload(7, "hello");
            await queue.AddToQueueAsync(payload);

            var roundTripped = await queue.GetNextAsync<SamplePayload>();
            Assert.Equal(payload, roundTripped);

            // Popping past the end of the queue takes the "no value" branch and yields the default.
            Assert.Null(await queue.GetNextAsync<SamplePayload>());
        }

        [Fact]
        public async Task PreservesFifoOrder()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddToQueueAsync("first");
            await queue.AddToQueueAsync("second");

            Assert.Equal("first", await queue.GetNextAsync());
            Assert.Equal("second", await queue.GetNextAsync());
            Assert.Null(await queue.GetNextAsync());
        }

        [Fact]
        public async Task AddRangeToQueueAsync_PushesAllValuesInOneCall_PreservingOrder()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            // A single multi-value LPUSH appends all values, left-to-right, so they pop back in FIFO order.
            await queue.AddRangeToQueueAsync(["first", "second", "third"]);

            Assert.Equal("first", await queue.GetNextAsync());
            Assert.Equal("second", await queue.GetNextAsync());
            Assert.Equal("third", await queue.GetNextAsync());
            Assert.Null(await queue.GetNextAsync());
        }

        [Fact]
        public async Task AddRangeToQueueAsync_AlreadyCancelled_ThrowsWithoutPushingAnything()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Unlike every other write on this queue, this one no longer just abandons an already-dispatched
            // command on cancellation (#2106) — a budget that is already spent fails fast before the LPUSH is
            // ever sent, so the caller can rely on nothing having landed.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.AddRangeToQueueAsync(["a", "b"], cts.Token));
            Assert.Null(await queue.GetNextAsync());
        }

        [Fact]
        public async Task AddRangeToQueueAsync_CancelledAfterTheCommandIsDispatched_StillCompletesAndPushesTheValues()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            using var cts = new CancellationTokenSource();

            // By the time this call returns a (necessarily incomplete) Task, it has already run synchronously
            // past the up-front cancellation check and dispatched the LPUSH to Redis — the only genuine await in
            // the method is on that in-flight network round trip. Cancelling immediately afterward therefore
            // deterministically lands in the mid-flight window #2106 closes: the push must not be abandoned once
            // dispatched, so it still completes and lands rather than throwing.
            var task = queue.AddRangeToQueueAsync(["a", "b"], cts.Token);
            await cts.CancelAsync();
            await task;

            Assert.Equal("a", await queue.GetNextAsync());
            Assert.Equal("b", await queue.GetNextAsync());
        }

        [Fact]
        public async Task ReserveNextAsync_ParksItemOffTheQueueUntilAcknowledged()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddRangeToQueueAsync(["a", "b"]);
            Assert.Equal(2, await queue.GetLengthAsync());

            // Reserving takes the head off the queue (so a concurrent drainer can't re-read it) but keeps it
            // recoverable on the processing list rather than destroying it.
            Assert.Equal("a", await queue.ReserveNextAsync());
            Assert.Equal(1, await queue.GetLengthAsync());

            // Acknowledging removes the reserved item for good; nothing remains to reclaim.
            await queue.AcknowledgeAsync("a");
            Assert.Equal(0, await queue.ReclaimProcessingAsync());

            Assert.Equal("b", await queue.ReserveNextAsync());
            await queue.AcknowledgeAsync("b");
            Assert.Null(await queue.ReserveNextAsync());
            Assert.Equal(0, await queue.GetLengthAsync());
        }

        [Fact]
        public async Task ReclaimProcessingAsync_RestoresUnacknowledgedItemsToHead_PreservingOrder()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddRangeToQueueAsync(["a", "b", "c"]);

            // Reserve two items without acknowledging them — modelling a run that crashed with work in flight.
            Assert.Equal("a", await queue.ReserveNextAsync());
            Assert.Equal("b", await queue.ReserveNextAsync());

            // Reclaim moves both orphaned items back ahead of the still-queued "c", in their original order.
            Assert.Equal(2, await queue.ReclaimProcessingAsync());
            Assert.Equal("a", await queue.GetNextAsync());
            Assert.Equal("b", await queue.GetNextAsync());
            Assert.Equal("c", await queue.GetNextAsync());
            Assert.Null(await queue.GetNextAsync());
        }

        [Fact]
        public async Task ReclaimProcessingAsync_DrainsTheWholeProcessingListInOneCall_PreservingOrder()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            // Reserve several items without acknowledging them, modelling a run that crashed with a backlog in
            // flight, so the single-round-trip Lua reclaim must move more than one item and keep their order.
            var items = Enumerable.Range(0, 8).Select(i => $"item-{i}").ToArray();
            await queue.AddRangeToQueueAsync(items);
            foreach (var item in items)
            {
                Assert.Equal(item, await queue.ReserveNextAsync());
            }

            Assert.Equal(items.Length, await queue.ReclaimProcessingAsync());

            // All reclaimed items return at the head in their original order, and a second reclaim finds nothing.
            foreach (var item in items)
            {
                Assert.Equal(item, await queue.GetNextAsync());
            }
            Assert.Null(await queue.GetNextAsync());
            Assert.Equal(0, await queue.ReclaimProcessingAsync());
        }

        [Fact]
        public async Task PeekAsync_ReturnsHeadItemsOldestFirst_WithoutRemovingThem()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddRangeToQueueAsync(["a", "b", "c"]);

            // A non-destructive read returns the oldest items first and leaves them in place.
            Assert.Equal(["a", "b"], await queue.PeekAsync(2));
            Assert.Equal(3, await queue.GetLengthAsync());

            // A count beyond the queue length returns everything still present (nothing was consumed).
            Assert.Equal(["a", "b", "c"], await queue.PeekAsync(10));
            Assert.Equal(3, await queue.GetLengthAsync());
        }

        [Fact]
        public async Task PeekAsync_NonPositiveCount_ReturnsEmptyWithoutTouchingTheQueue()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddToQueueAsync("a");

            Assert.Empty(await queue.PeekAsync(0));
            Assert.Empty(await queue.PeekAsync(-5));
            Assert.Equal(1, await queue.GetLengthAsync());
        }

        [Fact]
        public async Task PeekProcessingAsync_ReturnsHeadItemsOldestFirst_WithoutRemovingThem()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddRangeToQueueAsync(["a", "b", "c"]);
            Assert.Equal("a", await queue.ReserveNextAsync());
            Assert.Equal("b", await queue.ReserveNextAsync());

            // A non-destructive read returns the processing list's oldest (least-recently-reserved) item
            // first and leaves it in place.
            Assert.Equal(["a"], await queue.PeekProcessingAsync(1));
            Assert.Equal(["a", "b"], await queue.PeekProcessingAsync(10));
        }

        [Fact]
        public async Task PeekProcessingAsync_NonPositiveCount_ReturnsEmptyWithoutTouchingTheQueue()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddToQueueAsync("a");
            Assert.Equal("a", await queue.ReserveNextAsync());

            Assert.Empty(await queue.PeekProcessingAsync(0));
            Assert.Empty(await queue.PeekProcessingAsync(-5));
            Assert.Equal(["a"], await queue.PeekProcessingAsync(10));
        }

        [Fact]
        public async Task ReadReserveReclaimOps_WhenTokenAlreadyCancelled_ThrowOperationCanceled()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            // Seed an item so the ops have real work to do rather than short-circuiting on an empty queue.
            await queue.AddToQueueAsync("seed");

            // The read/reserve/reclaim ops now honour the per-command budget cooperatively (RedisCommandBudget):
            // an already-cancelled token unwinds the op up front rather than running the Redis round-trip, the
            // same prompt-cancellation contract the cache reads already satisfy (#558).
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.ReserveNextAsync(AlreadyCancelled()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.ReclaimProcessingAsync(AlreadyCancelled()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.GetLengthAsync(AlreadyCancelled()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.PeekAsync(1, AlreadyCancelled()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.PeekProcessingAsync(1, AlreadyCancelled()));

            // None of the throwing ops consumed the seeded item — a cancelled budget leaves the queue untouched.
            Assert.Equal("seed", await queue.GetNextAsync());
        }

        private static CancellationToken AlreadyCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            return cts.Token;
        }

        [Fact]
        public async Task RemoveAsync_RemovesASingleOccurrence_AndReportsWhetherOneWasRemoved()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            // Two copies of "dup" plus a "keep" — removing once must leave one "dup" behind.
            await queue.AddRangeToQueueAsync(["dup", "keep", "dup"]);

            Assert.True(await queue.RemoveAsync("dup"));
            Assert.Equal(["keep", "dup"], await queue.PeekAsync(10));

            // Removing a value that is no longer (or never was) present is a no-op.
            Assert.True(await queue.RemoveAsync("dup"));
            Assert.False(await queue.RemoveAsync("dup"));
            Assert.False(await queue.RemoveAsync("missing"));
            Assert.Equal(["keep"], await queue.PeekAsync(10));
        }

        [Fact]
        public async Task RemoveRangeAsync_RemovesEachRequestedOccurrenceInOneCall_RespectingDuplicateMultiplicity()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            // Two copies of "dup", one "solo", one "keep" — requesting "dup" twice must remove both copies
            // and requesting "solo" once must leave "keep" (never requested) untouched.
            await queue.AddRangeToQueueAsync(["dup", "solo", "keep", "dup"]);

            var removed = await queue.RemoveRangeAsync(["dup", "solo", "dup"]);

            Assert.Equal(3, removed);
            Assert.Equal(["keep"], await queue.PeekAsync(10));
        }

        [Fact]
        public async Task RemoveRangeAsync_SkipsValuesNoLongerPresent_CountingOnlyThoseActuallyRemoved()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            await queue.AddRangeToQueueAsync(["real"]);

            // "missing" was never on the queue and "real" is only requested for removal; the queue is not
            // mutated by a value that isn't actually present.
            var removed = await queue.RemoveRangeAsync(["real", "missing"]);

            Assert.Equal(1, removed);
            Assert.Empty(await queue.PeekAsync(10));
        }

        private sealed record SamplePayload(int Id, string Name);
    }
}
