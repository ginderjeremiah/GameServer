using Game.Abstractions.Infrastructure;
using Game.Infrastructure.PubSub.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Pins the registration-rollback invariant for the queue-based <see cref="RedisPubSubService"/>
    /// subscribe overloads (#655): when <c>SubscribeAsync</c> throws a transient connection error after the
    /// handle id was already added to the process-wide registry, the partial registration is rolled back, so a
    /// later retry with the same id is not wedged with "a handle already exists" while nothing is subscribed.
    /// <para>
    /// This needs no Redis server: a multiplexer pointed at a dead endpoint (<c>abortConnect=false</c>) connects
    /// lazily and then throws a real <see cref="RedisConnectionException"/> from <c>SubscribeAsync</c> — the exact
    /// transient failure the fix guards. The retry's outcome distinguishes fixed from broken deterministically:
    /// reaching <c>SubscribeAsync</c> again (another <see cref="RedisConnectionException"/>) proves the id was
    /// freed, whereas the unpatched code would throw <see cref="InvalidOperationException"/> at the registry add.
    /// It therefore stays an in-process test (no out-of-process dependency, like the sibling factory failure-path
    /// test), while the happy-path subscribe round-trips live in the Redis integration suite.
    /// </para>
    /// </summary>
    public class RedisPubSubSubscribeRollbackTests
    {
        // A refused/unreachable endpoint: abortConnect=false returns a non-connected multiplexer so the failure
        // surfaces at SubscribeAsync rather than at Connect, and the short timeout/no-retry keep the test quick.
        private const string DeadEndpoint = "127.0.0.1:1,abortConnect=false,connectTimeout=500,connectRetry=0";

        [Fact]
        public async Task Subscribe_ActionOverload_WhenSubscribeAsyncFails_RollsBackIdForRetry()
        {
            await AssertFailedSubscribeFreesId((service, id) =>
                service.Subscribe("rollback-channel", "rollback-queue", (Action<(IPubSubQueue queue, string channel)>)(_ => { }), id));
        }

        [Fact]
        public async Task Subscribe_AsyncOverload_WhenSubscribeAsyncFails_RollsBackIdForRetry()
        {
            await AssertFailedSubscribeFreesId((service, id) =>
                service.Subscribe("rollback-channel", "rollback-queue", (Func<(IPubSubQueue queue, string channel), Task>)(_ => Task.CompletedTask), id));
        }

        // Subscribes the same id twice against a failing connection. Both attempts must fail at SubscribeAsync
        // (RedisConnectionException); a second attempt failing at the registry (InvalidOperationException) would
        // mean the first failure wedged the id — the leak this rolls back.
        private static async Task AssertFailedSubscribeFreesId(Func<RedisPubSubService, string, Task> subscribe)
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(DeadEndpoint);
            var service = new RedisPubSubService(multiplexer, NullLoggerFactory.Instance);
            var id = $"rollback-{Guid.NewGuid()}";

            await Assert.ThrowsAsync<RedisConnectionException>(() => subscribe(service, id));

            // The retry must get past the registry add and fail at SubscribeAsync again — proving the id was freed.
            await Assert.ThrowsAsync<RedisConnectionException>(() => subscribe(service, id));
        }
    }
}
