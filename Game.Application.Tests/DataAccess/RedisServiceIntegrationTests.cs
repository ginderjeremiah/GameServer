using Game.Abstractions.Infrastructure;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Covers the Redis-backed <see cref="ICacheService"/> (RedisService) primitives that back the socket-
    /// presence claim and other atomic cache usage, and the null-handling contract on the value-accepting
    /// setters (#1015): a null value deletes the key for <c>Set</c>/<c>SetAndForget</c> (the de-facto behaviour
    /// the generic overloads rely on). RedisService is a thin adapter over an out-of-process dependency, so per
    /// the testing guidelines it is exercised through an integration test against the DI-resolved interface
    /// rather than mocked. Each test uses a unique key so it is independent of any residual cache state.
    /// </summary>
    [Collection("Integration")]
    public class RedisServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        private ConnectionMultiplexer? _readMultiplexer;

        public RedisServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        public override async ValueTask DisposeAsync()
        {
            if (_readMultiplexer is not null)
            {
                await _readMultiplexer.DisposeAsync();
            }

            await base.DisposeAsync();
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
            var value = await PollingHelper.PollUntilAsync(() => cache.Get(key), v => v is null);

            Assert.True(value is null, $"Expected the fire-and-forget null write to delete key '{key}' within the timeout.");
        }

        [Fact]
        public async Task GetAndRefreshExpiry_OnAnExistingKey_ReturnsValueAndRefreshesTtl()
        {
            var key = $"redis-getex-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await cache.Set(key, "player-a", TimeSpan.FromSeconds(2));

            var value = await cache.GetAndRefreshExpiry(key, TimeSpan.FromSeconds(30));

            // The value comes back unchanged and the TTL is refreshed past the 2s seed ceiling, in one round
            // trip rather than a separate awaited get followed by a fire-and-forget expire.
            Assert.Equal("player-a", value);
            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.FromSeconds(10), $"Expected the TTL to be refreshed past the 2s seed but was {ttl}.");
        }

        [Fact]
        public async Task GetAndRefreshExpiry_OnAMissingKey_ReturnsNullAndDoesNotCreateTheKey()
        {
            var key = $"redis-getex-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var value = await cache.GetAndRefreshExpiry(key, TimeSpan.FromSeconds(30));

            Assert.Null(value);
            Assert.Null(await cache.Get(key));
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

            var ttl = await PollingHelper.PollUntilAsync(() => ReadTtlAsync(key), t => t > TimeSpan.FromSeconds(10));

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
        public async Task HashSetAndForget_ThenReadBack_RoundTripsFieldsAndSetsTtl()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, TimeSpan.FromSeconds(30));

            var fields = await PollingHelper.PollUntilAsync(() => ReadHashAsync(key), f => f is not null);

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
            await WaitForHashFieldCountAsync(key, 2);

            // Only "a" is named this time — "b" must survive untouched and the TTL must be refreshed well
            // past the 2s seed ceiling.
            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "9" }, TimeSpan.FromSeconds(30));

            var fields = await PollingHelper.PollUntilAsync(() => ReadHashAsync(key), f => f?.GetValueOrDefault("a") == "9");

            Assert.NotNull(fields);
            Assert.Equal("9", fields["a"]);
            Assert.Equal("2", fields["b"]);

            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.FromSeconds(10), $"Expected the TTL to be refreshed past the 2s seed but was {ttl}.");
        }

        [Fact]
        public async Task HashSetAndForget_WithNoFields_IsANoOp()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetAndForget(key, new Dictionary<string, string>(), TimeSpan.FromSeconds(30));

            await Task.Delay(200);
            Assert.Null(await ReadHashAsync(key));
        }

        [Fact]
        public async Task HashSetAndForget_AfterScriptCacheIsFlushedMidRun_StillTakesEffect()
        {
            // Mirrors a Redis restart, failover, or manual SCRIPT FLUSH clearing the server-side script cache
            // mid-process (#2126). HashSetAndForget is fire-and-forget, so it gets no reply to inspect for
            // NOSCRIPT — this only survives the flush if it never trusted an assumed-cached script in the first
            // place, which is exactly the fix: it always sends the full script text.
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // Exercise the script at least once first, matching a long-lived process that already warmed it
            // before its Redis connection loses the script cache.
            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "1" }, TimeSpan.FromSeconds(30));
            await WaitForHashFieldCountAsync(key, 1);

            await FlushScriptCacheAsync();

            cache.HashSetAndForget(key, new Dictionary<string, string> { ["b"] = "2" }, TimeSpan.FromSeconds(30));

            var fields = await PollingHelper.PollUntilAsync(() => ReadHashAsync(key), f => f?.ContainsKey("b") == true);

            Assert.NotNull(fields);
            Assert.Equal("2", fields["b"]);
        }

        [Fact]
        public async Task HashSetIfExistsAndForget_OnAMissingKey_DoesNotCreateIt()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetIfExistsAndForget(key, new Dictionary<string, string> { ["a"] = "1" }, TimeSpan.FromSeconds(30));

            await Task.Delay(200);
            Assert.Null(await ReadHashAsync(key));
        }

        [Fact]
        public async Task HashSetIfExistsAndForget_OverAnExistingHash_OverwritesOnlyTheNamedFieldsAndRefreshesTtl()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, TimeSpan.FromSeconds(2));
            await WaitForHashFieldCountAsync(key, 2);

            cache.HashSetIfExistsAndForget(key, new Dictionary<string, string> { ["a"] = "9" }, TimeSpan.FromSeconds(30));

            var fields = await PollingHelper.PollUntilAsync(() => ReadHashAsync(key), f => f?.GetValueOrDefault("a") == "9");

            Assert.NotNull(fields);
            Assert.Equal("9", fields["a"]);
            Assert.Equal("2", fields["b"]);

            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.FromSeconds(10), $"Expected the TTL to be refreshed past the 2s seed but was {ttl}.");
        }

        [Fact]
        public async Task HashGetAllAndRefreshExpiry_OnAMissingKey_ReturnsNullAndDoesNotCreateTheKey()
        {
            var key = $"redis-hash-getex-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var fields = await cache.HashGetAllAndRefreshExpiry(key, TimeSpan.FromSeconds(30));

            Assert.Null(fields);
            Assert.Null(await ReadHashAsync(key));
        }

        [Fact]
        public async Task HashGetAllAndRefreshExpiry_OnAnExistingHash_ReturnsFieldsAndRefreshesTtlInOneRoundTrip()
        {
            var key = $"redis-hash-getex-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            cache.HashSetAndForget(key, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, TimeSpan.FromSeconds(2));
            await WaitForHashFieldCountAsync(key, 2);

            var fields = await cache.HashGetAllAndRefreshExpiry(key, TimeSpan.FromSeconds(30));

            Assert.NotNull(fields);
            Assert.Equal(2, fields.Count);
            Assert.Equal("1", fields["a"]);
            Assert.Equal("2", fields["b"]);

            // The TTL is refreshed well past the 2s seed ceiling, in the same round trip as the read rather
            // than a separate awaited HGETALL followed by a fire-and-forget expire.
            var ttl = await ReadTtlAsync(key);
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.FromSeconds(10), $"Expected the TTL to be refreshed past the 2s seed but was {ttl}.");
        }

        [Fact]
        public async Task HashGetAllAndRefreshExpiry_OnAKeyHoldingANonHashValue_TreatsItAsAMissAndClearsIt()
        {
            // Mirrors a key still holding a prior string-blob representation after a cache-shape change
            // (#1635 moved player progress from a serialized string to a Redis hash) — HGETALL would otherwise
            // error every read forever instead of self-healing on the next write.
            var key = $"redis-hash-getex-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await cache.Set(key, "a-stale-string-blob", TimeSpan.FromSeconds(30));

            Assert.Null(await cache.HashGetAllAndRefreshExpiry(key, TimeSpan.FromSeconds(30)));
            Assert.Null(await cache.Get(key));
        }

        [Fact]
        public async Task HashSetIfExistsAndForget_WithNoFields_IsANoOp()
        {
            var key = $"redis-hash-{Guid.NewGuid()}";
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            cache.HashSetIfExistsAndForget(key, new Dictionary<string, string>(), TimeSpan.FromSeconds(30));

            await Task.Delay(200);
            Assert.Null(await ReadHashAsync(key));
        }

        private async Task WaitForHashFieldCountAsync(string key, int count, int timeoutMs = 5000)
        {
            var fields = await PollingHelper.PollUntilAsync(() => ReadHashAsync(key), f => f?.Count == count, timeoutMs);
            if (fields?.Count != count)
            {
                Assert.Fail($"Expected hash '{key}' to have {count} fields within the timeout.");
            }
        }

        private static async Task<bool> WaitUntilValueEqualsAsync(ICacheService cache, string key, string expected, int timeoutMs = 5000)
        {
            var value = await PollingHelper.PollUntilAsync(() => cache.Get(key), v => v == expected, timeoutMs);
            return value == expected;
        }

        private async Task<TimeSpan?> ReadTtlAsync(string key)
        {
            var multiplexer = await GetReadMultiplexerAsync();
            return await multiplexer.GetDatabase().KeyTimeToLiveAsync(key);
        }

        // A side-effect-free raw HGETALL, independent of ICacheService's own hash-read methods, so tests that
        // merely verify state written by HashSetAndForget/HashSetIfExistsAndForget don't accidentally exercise
        // (or mask a bug in) HashGetAllAndRefreshExpiry's own TTL-refreshing side effect.
        private async Task<Dictionary<string, string>?> ReadHashAsync(string key)
        {
            var multiplexer = await GetReadMultiplexerAsync();
            var db = multiplexer.GetDatabase();
            if (!await db.KeyExistsAsync(key))
            {
                return null;
            }

            var entries = await db.HashGetAllAsync(key);
            return entries.ToDictionary(e => (string)e.Name!, e => (string)e.Value!);
        }

        // Shared across ReadTtlAsync/ReadHashAsync (frequently called inside PollUntilAsync loops) so a
        // slow-settling assertion reuses one connection instead of churning through a fresh one per call.
        private async Task<ConnectionMultiplexer> GetReadMultiplexerAsync()
        {
            if (_readMultiplexer is null)
            {
                _readMultiplexer = await ConnectionMultiplexer.ConnectAsync(Containers.CacheConnectionString);
            }

            return _readMultiplexer;
        }

        private async Task FlushScriptCacheAsync()
        {
            var options = ConfigurationOptions.Parse(Containers.CacheConnectionString);
            options.AllowAdmin = true;
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
            await multiplexer.GetServers().First().ScriptFlushAsync();
        }
    }
}
