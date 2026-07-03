using Game.Abstractions.Infrastructure;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Covers the keyed-subscription registry branches of the Redis-backed <see cref="IPubSubService"/>
    /// (RedisPubSubService) that the write-behind/backplane integration tests don't reach: rejecting a
    /// duplicate handle id (across both the plain and the queue/worker overloads), the remove-vs-unknown
    /// branches of <see cref="IPubSubService.UnSubscribe(string, string)"/>, and the batched-publish path
    /// (<see cref="IPubSubService.PublishBatch{T}"/> — the multi-value LPUSH and its empty no-op). These are
    /// genuine logic (not connection-failure simulation), so per the testing guidelines they are pinned
    /// through an integration test against the DI-resolved service rather than mocked. Each test uses unique
    /// channel/queue/id values because the handle registry and queues are process-wide.
    /// </summary>
    [Collection("Integration")]
    public class RedisPubSubServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        public RedisPubSubServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task PublishBatch_SerializesAndEnqueuesEveryItem_InOrder()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var channel = $"pubsub-test-{Guid.NewGuid()}";
            var queueName = $"pubsub-queue-{Guid.NewGuid()}";

            var payloads = new[]
            {
                new SamplePayload(1, "alpha"),
                new SamplePayload(2, "beta"),
                new SamplePayload(3, "gamma"),
            };

            // The whole batch lands on the queue via one multi-value LPUSH, in order, ready for the consumer.
            await pubsub.PublishBatch(channel, queueName, payloads);

            var queue = pubsub.GetQueue(queueName);
            Assert.Equal(payloads[0], await queue.GetNextAsync<SamplePayload>());
            Assert.Equal(payloads[1], await queue.GetNextAsync<SamplePayload>());
            Assert.Equal(payloads[2], await queue.GetNextAsync<SamplePayload>());
            Assert.Null(await queue.GetNextAsync<SamplePayload>());
        }

        [Fact]
        public async Task PublishBatch_WithEmptyBatch_IsNoOp()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var channel = $"pubsub-test-{Guid.NewGuid()}";
            var queueName = $"pubsub-queue-{Guid.NewGuid()}";

            // An empty batch must not touch the queue (an empty RPUSH would otherwise be an error).
            await pubsub.PublishBatch(channel, queueName, Array.Empty<SamplePayload>());

            Assert.Null(await pubsub.GetQueue(queueName).GetNextAsync<SamplePayload>());
        }

        private sealed record SamplePayload(int Id, string Name);

        [Fact]
        public async Task Subscribe_WithDuplicateId_ThrowsInvalidOperation()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var channel = $"pubsub-test-{Guid.NewGuid()}";
            var id = $"handle-{Guid.NewGuid()}";

            await pubsub.Subscribe(channel, _ => { }, id);
            try
            {
                // The second registration of the same id is rejected before it can shadow the first handle.
                await Assert.ThrowsAsync<InvalidOperationException>(() => pubsub.Subscribe(channel, _ => { }, id));
            }
            finally
            {
                await pubsub.UnSubscribe(channel, id);
            }
        }

        [Fact]
        public async Task Subscribe_QueueOverload_WithDuplicateId_ThrowsInvalidOperation()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var channel = $"pubsub-test-{Guid.NewGuid()}";
            var queueName = $"pubsub-queue-{Guid.NewGuid()}";
            var id = $"handle-{Guid.NewGuid()}";
            Func<(IPubSubQueue queue, string channel), Task> handler = _ => Task.CompletedTask;

            await pubsub.Subscribe(channel, queueName, handler, id);
            try
            {
                // The duplicate registration must dispose the worker it speculatively created before throwing.
                await Assert.ThrowsAsync<InvalidOperationException>(() => pubsub.Subscribe(channel, queueName, handler, id));
            }
            finally
            {
                await pubsub.UnSubscribe(channel, id);
            }
        }

        [Fact]
        public async Task UnSubscribe_RemovesHandle_AllowingReuseOfId()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var channel = $"pubsub-test-{Guid.NewGuid()}";
            var id = $"handle-{Guid.NewGuid()}";

            await pubsub.Subscribe(channel, _ => { }, id);
            await pubsub.UnSubscribe(channel, id);

            // Re-subscribing with the same id only succeeds because UnSubscribe removed the prior handle.
            await pubsub.Subscribe(channel, _ => { }, id);
            await pubsub.UnSubscribe(channel, id);
        }

        [Fact]
        public async Task UnSubscribe_WithUnknownId_IsNoOp()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();

            // Removing an id that was never registered takes the not-found branch and must not throw.
            await pubsub.UnSubscribe($"pubsub-test-{Guid.NewGuid()}", $"missing-{Guid.NewGuid()}");
        }
    }
}
