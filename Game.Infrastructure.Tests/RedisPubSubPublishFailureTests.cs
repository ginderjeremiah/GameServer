using Game.Infrastructure.PubSub.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Pins the fix for #1888: <see cref="RedisPubSubService.Publish(string, string, System.Threading.CancellationToken)"/>
    /// no longer sends with <see cref="CommandFlags.FireAndForget"/>, so a genuine send failure (the caller's
    /// only signal that a broadcast with no durable backing — e.g. the reference-data invalidation channel —
    /// was lost) now propagates as an exception instead of vanishing silently.
    /// <para>
    /// Uses the same dead-endpoint technique as the sibling <see cref="RedisPubSubSubscribeRollbackTests"/>: a
    /// multiplexer pointed at an unreachable endpoint (<c>abortConnect=false</c>) connects lazily and then
    /// throws a real <see cref="RedisConnectionException"/> from the command — no mocking, no Redis server
    /// required, matching the project's "genuine dependency failure" test style.
    /// </para>
    /// </summary>
    public class RedisPubSubPublishFailureTests
    {
        private const string DeadEndpoint = "127.0.0.1:1,abortConnect=false,connectTimeout=500,connectRetry=0";

        [Fact]
        public async Task Publish_WhenSendFails_ThrowsInsteadOfSwallowingTheFailure()
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(DeadEndpoint);
            var service = new RedisPubSubService(multiplexer, NullLoggerFactory.Instance);

            // Fire-and-forget would let this complete successfully despite the unreachable connection, hiding
            // exactly the failure #1888 is about; the fix must let it throw instead.
            await Assert.ThrowsAsync<RedisConnectionException>(() => service.Publish("referenceData", "some-instance-id"));
        }
    }
}
