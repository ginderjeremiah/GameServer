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
    /// duplicate handle id (across both the plain and the queue/worker overloads) and the remove-vs-unknown
    /// branches of <see cref="IPubSubService.UnSubscribe(string, string)"/>. These are genuine registry logic
    /// (not connection-failure simulation), so per the testing guidelines they are pinned through an
    /// integration test against the DI-resolved service rather than mocked. Each test uses unique
    /// channel/id values because the handle registry is process-wide.
    /// </summary>
    [Collection("Integration")]
    public class RedisPubSubServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        public RedisPubSubServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

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
            Action<(IPubSubQueue queue, string channel)> handler = _ => { };

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
