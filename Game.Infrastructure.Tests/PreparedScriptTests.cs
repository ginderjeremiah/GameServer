using Game.Infrastructure.Redis;
using StackExchange.Redis;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Unit tests for <see cref="PreparedScript"/>, the helper that lets an awaited caller send a Lua script's
    /// hash instead of its full text on every call after the first (#2056). The behaviour that matters and is
    /// otherwise only exercised indirectly through a live Redis: a cold call that fails before it reaches the
    /// server must not mark the script warmed, or every later call would jump straight to an <c>EVALSHA</c> the
    /// server has genuinely never cached. <see cref="PreparedScript.Evaluate"/> (the fire-and-forget-capable sync
    /// path) has no such state to pin — it always sends the full script text (#2126) — so it isn't covered here;
    /// its NOSCRIPT-survival behaviour is covered by the integration suite instead
    /// (RedisServiceIntegrationTests.HashSetAndForget_AfterScriptCacheIsFlushedMidRun_StillTakesEffect).
    /// <para>
    /// Uses the same dead-endpoint technique as the sibling <see cref="RedisPubSubPublishFailureTests"/>: a
    /// multiplexer pointed at an unreachable endpoint (<c>abortConnect=false</c>) connects lazily and then throws
    /// a real <see cref="RedisConnectionException"/> from the awaited command — no mocking, no Redis server
    /// required. The NOSCRIPT-triggered fallback itself needs a genuine server reply and is covered by the
    /// existing Redis integration suite exercising these scripts end to end (RedisServiceIntegrationTests,
    /// RedisQueueIntegrationTests) rather than duplicated here.
    /// </para>
    /// </summary>
    public class PreparedScriptTests
    {
        private const string DeadEndpoint = "127.0.0.1:1,abortConnect=false,connectTimeout=500,connectRetry=0";

        [Fact]
        public async Task EvaluateAsync_WhenTheColdCallFails_LeavesTheScriptUnwarmed()
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(DeadEndpoint);
            var db = multiplexer.GetDatabase();
            var script = new PreparedScript("return 1");

            await Assert.ThrowsAsync<RedisConnectionException>(() => script.EvaluateAsync(db, [], []));

            // A failed cold call means Redis never actually cached this script; if it had wrongly been marked
            // warmed anyway, every later call would send only the hash and get NOSCRIPT forever.
            Assert.False(script.IsWarmedForTesting);
        }

        [Fact]
        public async Task EvaluateAsync_WhenAlreadyWarmed_StaysWarmedAfterAFailedCall()
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(DeadEndpoint);
            var db = multiplexer.GetDatabase();
            var script = PreparedScript.CreateAlreadyWarmedForTesting("return 1");

            // A dead endpoint can't distinguish the hash branch from the full-text one — both throw the same
            // RedisConnectionException before anything reaches a server — so this doesn't pin which command form
            // was sent (that needs a genuine NOSCRIPT reply, covered by the Redis integration suites instead).
            // What it does pin: CreateAlreadyWarmedForTesting actually starts warmed, and a failure afterward
            // doesn't flip it back to cold.
            Assert.True(script.IsWarmedForTesting);
            await Assert.ThrowsAsync<RedisConnectionException>(() => script.EvaluateAsync(db, [], []));
            Assert.True(script.IsWarmedForTesting);
        }
    }
}
