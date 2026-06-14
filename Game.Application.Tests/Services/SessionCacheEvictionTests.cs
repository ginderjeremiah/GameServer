using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.DataAccess;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Covers the session-cache eviction policy (#537): the cached <c>Session_{userId}</c> key (the in-flight
    /// <see cref="PlayerState"/>) is written with a sliding idle TTL rather than living forever, mirroring the
    /// player-aggregate policy (#439). Unlike the player key the session has no DB reload path, so the TTL is the
    /// generous refresh-token lifetime and is slid on every read. These exercise the fire-and-forget cache writes
    /// through the DI-resolved store, so the TTL assertions poll Redis until the write lands.
    /// </summary>
    [Collection("Integration")]
    public class SessionCacheEvictionTests : ApplicationIntegrationTestBase
    {
        // Generous lower bound: the 48h TTL only ever decreases slightly between being set and read, so any
        // value above a day proves a real expiry was attached (vs. the old unbounded SetAndForget).
        private static readonly TimeSpan TtlFloor = TimeSpan.FromHours(24);

        public SessionCacheEvictionTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task Update_WritesSessionWithIdleTtl()
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            var db = multiplexer.GetDatabase();
            const int userId = 4101;
            var sessionKey = SessionKey(userId);

            sessionStore.Update(new PlayerState { PlayerId = 7 }, userId);

            var ttl = await WaitForTtlAsync(db, sessionKey);
            Assert.NotNull(ttl);
            Assert.True(ttl > TtlFloor, $"expected a generous idle TTL, got {ttl}");
        }

        [Fact]
        public async Task GetSession_OnHit_RefreshesIdleTtl()
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            var db = multiplexer.GetDatabase();
            const int userId = 4102;
            var sessionKey = SessionKey(userId);

            // Prime the session, then shrink the TTL to a sliver to stand in for a key that has aged.
            sessionStore.Update(new PlayerState { PlayerId = 9 }, userId);
            await WaitForTtlAsync(db, sessionKey);
            await db.KeyExpireAsync(sessionKey, TimeSpan.FromSeconds(30));

            // A read hit must slide the TTL back up to the full idle budget and round-trip the state.
            var session = await sessionStore.GetSession(userId);
            Assert.NotNull(session);
            Assert.Equal(9, session.PlayerId);

            var ttl = await WaitForTtlAsync(db, sessionKey, predicate: t => t > TtlFloor);
            Assert.NotNull(ttl);
            Assert.True(ttl > TtlFloor, $"expected the hit to refresh the TTL, got {ttl}");
        }

        [Fact]
        public async Task GetSession_OnMiss_ReturnsNullAndCreatesNoKey()
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            var db = multiplexer.GetDatabase();
            const int userId = 4103;
            var sessionKey = SessionKey(userId);

            // There is no DB reload path for a session, so a miss is simply null and the slide is a no-op on
            // the missing key (ExpireAndForget must not resurrect it).
            var session = await sessionStore.GetSession(userId);

            Assert.Null(session);
            Assert.False(await db.KeyExistsAsync(sessionKey));
        }

        private static string SessionKey(int userId) => $"{Constants.CACHE_SESSION_PREFIX}_{userId}";

        // Polls the key's TTL until it satisfies the predicate (defaults to "any TTL is set"), tolerating the
        // fire-and-forget write not having landed yet. KeyTimeToLiveAsync returns null both for a missing key
        // and a key with no expiry, so a non-null result proves an expiry is attached.
        private async Task<TimeSpan?> WaitForTtlAsync(IDatabase db, string key, Func<TimeSpan, bool>? predicate = null)
        {
            predicate ??= _ => true;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            TimeSpan? ttl = null;
            while (DateTime.UtcNow < deadline)
            {
                ttl = await db.KeyTimeToLiveAsync(key);
                if (ttl is not null && predicate(ttl.Value))
                {
                    return ttl;
                }

                await Task.Delay(25, CancellationToken);
            }

            return ttl;
        }
    }
}
