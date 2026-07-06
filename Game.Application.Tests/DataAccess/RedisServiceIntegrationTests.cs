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
    /// key for <c>Set</c>/<c>SetAndForget</c> (the de-facto behaviour the generic overloads rely on), while
    /// <c>GetSet</c> has no such path and is non-null. RedisService is a thin adapter over an out-of-process
    /// dependency, so per the testing guidelines it is exercised through an integration test against the
    /// DI-resolved interface rather than mocked. Each test uses a unique key so it is independent of any residual
    /// cache state.
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
        public async Task SetAndForgetWithExpiry_WithNullValue_DeletesTheKey()
        {
            var key = $"redis-setforget-expiry-null-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cache.Set(key, "occupied", TimeSpan.FromSeconds(30));

            // Fire-and-forget, so the delete settles asynchronously — poll until the key is gone rather than
            // racing a fixed delay.
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

        [Fact]
        public async Task ReclaimAndForget_OnAMissingKey_ClaimsItWithTtl()
        {
            // Mirrors a live socket's presence key having lapsed (TTL expiry or a registration rollback) while
            // it is still the genuine owner — the resurrection case ExpireAndForget can't handle (#1497).
            var key = $"redis-reclaim-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.ReclaimAndForget(key, "owner-a", TimeSpan.FromSeconds(30));

            Assert.True(await WaitUntilValueEqualsAsync(cache, key, "owner-a"));
            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.Zero && ttl <= TimeSpan.FromSeconds(30), $"Expected a positive TTL within 30s but was {ttl}.");
        }

        [Fact]
        public async Task ReclaimAndForget_OnAKeyItAlreadyOwns_RefreshesTtlWithoutChangingValue()
        {
            var key = $"redis-reclaim-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await cache.Set(key, "owner-a", TimeSpan.FromSeconds(2));

            cache.ReclaimAndForget(key, "owner-a", TimeSpan.FromSeconds(30));

            var deadline = DateTime.UtcNow.AddSeconds(5);
            TimeSpan? ttl = null;
            while (DateTime.UtcNow < deadline)
            {
                ttl = await ReadTtlAsync(key);
                if (ttl > TimeSpan.FromSeconds(10))
                {
                    break;
                }

                await Task.Delay(25);
            }

            Assert.True(ttl > TimeSpan.FromSeconds(10), $"Expected the TTL to be extended past its 2s seed but was {ttl}.");
            Assert.Equal("owner-a", await cache.Get(key));
        }

        [Fact]
        public async Task ReclaimAndForget_OnAKeyOwnedBySomeoneElse_DoesNotOverwriteIt()
        {
            // The no-clobber guarantee: a stale owner's heartbeat must never steal the key back from a newer
            // claimant, exactly like the existing takeover-safety behavior on ExpireAndForget.
            var key = $"redis-reclaim-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await cache.Set(key, "owner-b", TimeSpan.FromSeconds(30));

            cache.ReclaimAndForget(key, "owner-a", TimeSpan.FromSeconds(30));

            await Task.Delay(500);
            Assert.Equal("owner-b", await cache.Get(key));
        }

        [Fact]
        public async Task HashGetAllIfExists_OnAMissingKey_ReturnsNull()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            Assert.Null(await cache.HashGetAllIfExists(key));
        }

        [Fact]
        public async Task HashSetAndForget_ThenHashGetAllIfExists_RoundTripsFieldsAndSetsTtl()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, TimeSpan.FromSeconds(30));

            Dictionary<string, string>? fields = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                fields = await cache.HashGetAllIfExists(key);
                if (fields is not null)
                {
                    break;
                }

                await Task.Delay(25);
            }

            Assert.NotNull(fields);
            Assert.Equal(2, fields.Count);
            Assert.Equal("1", fields["a"]);
            Assert.Equal("2", fields["b"]);

            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.Zero && ttl <= TimeSpan.FromSeconds(30), $"Expected a positive TTL within 30s but was {ttl}.");
        }

        [Fact]
        public async Task HashSetAndForget_OverAnExistingHash_OverwritesOnlyTheNamedFieldsAndRefreshesTtl()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, TimeSpan.FromSeconds(2));
            await WaitForHashFieldCountAsync(cache, key, 2);

            // Only "a" is named this time — "b" must survive untouched and the TTL must be refreshed well
            // past the 2s seed ceiling.
            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "9" }, TimeSpan.FromSeconds(30));

            Dictionary<string, string>? fields = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                fields = await cache.HashGetAllIfExists(key);
                if (fields?.GetValueOrDefault("a") == "9")
                {
                    break;
                }

                await Task.Delay(25);
            }

            Assert.NotNull(fields);
            Assert.Equal("9", fields["a"]);
            Assert.Equal("2", fields["b"]);

            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.FromSeconds(10), $"Expected the TTL to be refreshed past the 2s seed but was {ttl}.");
        }

        [Fact]
        public async Task HashGetAllIfExists_OnAKeyHoldingANonHashValue_TreatsItAsAMissAndClearsIt()
        {
            // Mirrors a key still holding a prior string-blob representation after a cache-shape change
            // (#1635 moved player progress from a serialized string to a Redis hash) — HGETALL would otherwise
            // error every read forever instead of self-healing on the next write.
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await cache.Set(key, "a-stale-string-blob", TimeSpan.FromSeconds(30));

            Assert.Null(await cache.HashGetAllIfExists(key));
            Assert.Null(await cache.Get(key));
        }

        [Fact]
        public async Task HashSetAndForget_WithNoFields_IsANoOp()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetAndForget(key, new Dictionary<string, string>(), TimeSpan.FromSeconds(30));

            await Task.Delay(200);
            Assert.Null(await cache.HashGetAllIfExists(key));
        }

        private static async Task WaitForHashFieldCountAsync(ICacheService cache, string key, int count, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if ((await cache.HashGetAllIfExists(key))?.Count == count)
                {
                    return;
                }

                await Task.Delay(25);
            }

            Assert.Fail($"Expected hash '{key}' to have {count} fields within the timeout.");
        }

        private static async Task<bool> WaitUntilValueEqualsAsync(ICacheService cache, string key, string expected, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (await cache.Get(key) == expected)
                {
                    return true;
                }

                await Task.Delay(25);
            }

            return await cache.Get(key) == expected;
        }

        private async Task<TimeSpan?> ReadTtlAsync(string key)
        {
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            return await multiplexer.GetDatabase().KeyTimeToLiveAsync(key);
        }
    }
}
