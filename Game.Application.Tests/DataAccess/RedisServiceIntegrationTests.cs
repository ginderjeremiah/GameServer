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
    /// its TTL — and the null-handling contract on the value-accepting setters (#1015): a null value deletes the
    /// key for <c>Set</c>/<c>SetAndForget</c> (the de-facto behaviour the generic overloads rely on), while the
    /// non-expiry <c>GetSet</c> has no such path and is non-null. RedisService is a thin adapter over an
    /// out-of-process dependency, so per the testing guidelines it is exercised through an integration test
    /// against the DI-resolved interface rather than mocked. Each test uses a unique key so it is independent of
    /// any residual cache state.
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

        [Fact]
        public async Task GetSet_StoresTheNewValueAndReturnsThePriorValue()
        {
            var key = $"redis-getset-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cache.Set(key, "occupied");

            // Unlike Set/SetAndForget the non-expiry GetSet has no null-means-delete path (its underlying GETSET
            // rejects null), so its ICacheService contract is non-null — it always returns the prior value and
            // stores the new one.
            var prior = await cache.GetSet(key, "replacement");

            Assert.Equal("occupied", prior);
            Assert.Equal("replacement", await cache.Get(key));
        }

        [Fact]
        public async Task Set_WithNullValue_DeletesTheKey()
        {
            var key = $"redis-set-null-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cache.Set(key, "occupied");

            // A null value deletes the key — the null-means-delete contract the generic Set<T> depends on.
            await cache.Set(key, null);

            Assert.Null(await cache.Get(key));
        }

        [Fact]
        public async Task SetWithExpiry_WithNullValue_DeletesTheKey()
        {
            var key = $"redis-set-expiry-null-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cache.Set(key, "occupied", TimeSpan.FromSeconds(30));

            // The expiry-carrying Set keeps the same null-means-delete path (the TTL is irrelevant for a delete).
            await cache.Set(key, null, TimeSpan.FromSeconds(30));

            Assert.Null(await cache.Get(key));
        }

        [Fact]
        public async Task SetAndForget_WithNullValue_DeletesTheKey()
        {
            var key = $"redis-setforget-null-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cache.Set(key, "occupied");

            // Fire-and-forget, so the delete settles asynchronously — poll until the key is gone rather than
            // racing a fixed delay.
            cache.SetAndForget(key, null);

            await AssertKeyEventuallyDeletedAsync(cache, key);
        }

        [Fact]
        public async Task SetAndForgetWithExpiry_WithNullValue_DeletesTheKey()
        {
            var key = $"redis-setforget-expiry-null-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cache.Set(key, "occupied", TimeSpan.FromSeconds(30));

            cache.SetAndForget(key, null, TimeSpan.FromSeconds(30));

            await AssertKeyEventuallyDeletedAsync(cache, key);
        }

        private static async Task AssertKeyEventuallyDeletedAsync(ICacheService cache, string key)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (await cache.Get(key) is null)
                {
                    return;
                }

                await Task.Delay(25);
            }

            Assert.Fail($"Expected the fire-and-forget null write to delete key '{key}' within the timeout.");
        }

        private async Task<TimeSpan?> ReadTtlAsync(string key)
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            return await multiplexer.GetDatabase().KeyTimeToLiveAsync(key);
        }
    }
}
