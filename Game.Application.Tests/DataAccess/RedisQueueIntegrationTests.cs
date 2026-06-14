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

        private sealed record SamplePayload(int Id, string Name);
    }
}
