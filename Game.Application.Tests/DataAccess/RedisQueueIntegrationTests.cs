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
        public void TypedRoundTripSync_SerializesAndDeserializes_ThenReturnsNullWhenDrained()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = pubsub.GetQueue($"redis-queue-test-{Guid.NewGuid()}");

            var payload = new SamplePayload(9, "world");
            queue.AddToQueue(payload);

            var roundTripped = queue.GetNext<SamplePayload>();
            Assert.Equal(payload, roundTripped);

            Assert.Null(queue.GetNext<SamplePayload>());
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

        private sealed record SamplePayload(int Id, string Name);
    }
}
