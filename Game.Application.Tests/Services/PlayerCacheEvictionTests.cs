using Game.Abstractions.DataAccess;
using Game.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Covers the player-cache eviction policy (#439): the cached <c>Player</c> aggregate is written with a
    /// sliding idle TTL rather than living forever, and an expired/evicted key transparently reloads from the
    /// database. These exercise the fire-and-forget cache writes through the DI-resolved repository, so the
    /// TTL assertions poll Redis until the write lands.
    /// </summary>
    [Collection("Integration")]
    public class PlayerCacheEvictionTests : ApplicationIntegrationTestBase
    {
        // Generous lower bound: the 48h TTL only ever decreases slightly between being set and read, so any
        // value above a day proves a real expiry was attached (vs. the old unbounded SetAndForget).
        private static readonly TimeSpan TtlFloor = TimeSpan.FromHours(24);

        public PlayerCacheEvictionTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetPlayer_OnCacheMiss_CachesPlayerWithIdleTtl()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            var db = multiplexer.GetDatabase();
            var playerKey = PlayerKey(playerEntity.Id);

            // Cold load: cache miss falls through to the DB and re-caches with a TTL.
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var ttl = await WaitForTtlAsync(db, playerKey);
            Assert.NotNull(ttl);
            Assert.True(ttl > TtlFloor, $"expected a generous idle TTL, got {ttl}");
        }

        [Fact]
        public async Task SavePlayer_CachesPlayerWithIdleTtl()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            var db = multiplexer.GetDatabase();
            var playerKey = PlayerKey(playerEntity.Id);

            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);
            await db.KeyDeleteAsync(playerKey);

            player.ChangeZone(1);
            await playerRepo.SavePlayer(player);

            var ttl = await WaitForTtlAsync(db, playerKey);
            Assert.NotNull(ttl);
            Assert.True(ttl > TtlFloor, $"expected a generous idle TTL, got {ttl}");
        }

        [Fact]
        public async Task GetPlayer_OnCacheHit_RefreshesIdleTtl()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            var db = multiplexer.GetDatabase();
            var playerKey = PlayerKey(playerEntity.Id);

            // Prime the cache, then shrink the TTL to a sliver to stand in for a key that has aged.
            await playerRepo.GetPlayer(playerEntity.Id);
            await WaitForTtlAsync(db, playerKey);
            await db.KeyExpireAsync(playerKey, TimeSpan.FromSeconds(30));

            // A cache hit must slide the TTL back up to the full idle budget.
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var ttl = await WaitForTtlAsync(db, playerKey, predicate: t => t > TtlFloor);
            Assert.NotNull(ttl);
            Assert.True(ttl > TtlFloor, $"expected the hit to refresh the TTL, got {ttl}");
        }

        [Fact]
        public async Task GetPlayer_AfterKeyExpires_ReloadsFromDbAndReCaches()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Evicted");

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            var db = multiplexer.GetDatabase();
            var playerKey = PlayerKey(playerEntity.Id);

            // Prime the cache, then delete the key to simulate the TTL lapsing / an eviction.
            await playerRepo.GetPlayer(playerEntity.Id);
            await WaitForTtlAsync(db, playerKey);
            await db.KeyDeleteAsync(playerKey);
            Assert.False(await db.KeyExistsAsync(playerKey));

            // The expired key falls through to the DB transparently and re-caches with a fresh TTL.
            var reloaded = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("Evicted", reloaded.Name);

            var ttl = await WaitForTtlAsync(db, playerKey);
            Assert.NotNull(ttl);
            Assert.True(ttl > TtlFloor, $"expected the reload to re-cache with a TTL, got {ttl}");
        }

        private static string PlayerKey(int playerId) => $"{Constants.CACHE_PLAYER_PREFIX}_{playerId}";

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
