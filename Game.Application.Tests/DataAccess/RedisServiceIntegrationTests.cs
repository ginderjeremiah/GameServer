using Game.Abstractions.Infrastructure;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Covers the Redis-backed <see cref="ICacheService"/> (RedisService) atomic set-with-expiry-returning-old
    /// primitive (#691) — the one the socket-presence claim relies on so the key can never be written without
    /// its TTL. RedisService is a thin adapter over an out-of-process dependency, so per the testing guidelines
    /// it is exercised through an integration test against the DI-resolved interface rather than mocked. Each
    /// test uses a unique key so it is independent of any residual cache state.
    /// </summary>
    [Collection("Integration")]
    public class RedisServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        public RedisServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetSetWithExpiry_OnAFreshKey_WritesValueWithTtlAndReturnsNull()
        {
            var key = $"redis-getset-ttl-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // No prior owner, so the old value is null...
            var prior = await cache.GetSet(key, "socket-a", TimeSpan.FromSeconds(30));
            Assert.Null(prior);

            // ...and crucially the value and its TTL landed together — the key carries a positive expiry rather
            // than the never-expiring state the old GetSet-then-Expire pair leaves if it faults between calls.
            Assert.Equal("socket-a", await cache.Get(key));
            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.Zero && ttl <= TimeSpan.FromSeconds(30), $"Expected a positive TTL within 30s but was {ttl}.");
        }

        [Fact]
        public async Task GetSetWithExpiry_OverAnExistingKey_ReturnsPriorValueAndReplacesItWithRefreshedTtl()
        {
            var key = $"redis-getset-ttl-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // Seed a value with a short TTL so the refresh on the second write is observable.
            await cache.Set(key, "socket-a", TimeSpan.FromSeconds(2));

            var prior = await cache.GetSet(key, "socket-b", TimeSpan.FromSeconds(30));

            // The takeover returns the prior owner, swaps in the new value, and resets the TTL well past the 2s
            // seed ceiling — proving the expiry is (re)applied atomically with the write.
            Assert.Equal("socket-a", prior);
            Assert.Equal("socket-b", await cache.Get(key));
            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.FromSeconds(10), $"Expected the TTL to be refreshed past the 2s seed but was {ttl}.");
        }

        private async Task<TimeSpan?> ReadTtlAsync(string key)
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            return await multiplexer.GetDatabase().KeyTimeToLiveAsync(key);
        }
    }
}
