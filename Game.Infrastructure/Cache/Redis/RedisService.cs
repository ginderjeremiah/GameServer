using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Game.Infrastructure.Cache.Redis
{
    internal class RedisService : ICacheService
    {
        private readonly ILogger<RedisService> _logger;

        // The multiplexer is fixed for the instance lifetime, so the database handle it hands out is resolved once
        // in the ctor rather than per call on the hot cache get/set paths (#954).
        public IDatabase Redis { get; }

        public RedisService(IConnectionMultiplexer multiplexer, ILogger<RedisService> logger)
        {
            Redis = multiplexer.GetDatabase();
            _logger = logger;
        }

        // StackExchange.Redis exposes no CancellationToken on its database operations, so each async op honours the
        // per-command budget cooperatively via RedisCommandBudget (#558): pure reads (Get) take the read path,
        // while every mutating call — including the read-modify-write GetSet/GetDelete — routes through ObserveWrite
        // so a post-cancellation fault on the abandoned write is logged rather than silently lost.
        private const string WriteFaultMessage = "A Redis write faulted after its command budget was cancelled; the write may not have been applied.";

        public async Task<string?> Get(string key, CancellationToken cancellationToken = default)
        {
            return await RedisCommandBudget.Read(Redis.StringGetAsync(key), cancellationToken);
        }

        public async Task<T?> Get<T>(string key, CancellationToken cancellationToken = default)
        {
            var val = await Get(key, cancellationToken);
            return val.Deserialize<T>();
        }

        public async Task<string?> GetDelete(string key, CancellationToken cancellationToken = default)
        {
            // Read-and-delete is a write (it removes the key), so it routes through ObserveWrite too.
            return await ObserveWrite(Redis.StringGetDeleteAsync(key), cancellationToken);
        }

        public async Task<T?> GetDelete<T>(string key, CancellationToken cancellationToken = default)
        {
            var val = await GetDelete(key, cancellationToken);
            return val.Deserialize<T>();
        }

        public async Task<string?> GetSet(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            // The value is required (non-null) so the Lua SET below never receives a nil ARGV and errors
            // server-side: writing a TTL implies writing a value, so unlike the other setters this overload has
            // no null-means-delete path. The signature now matches ICacheService's non-null contract (#954).
            // Read-old-then-write in one Lua script (atomic, mirroring CompareAndDelete) so the value and its
            // TTL land together — a separate StringGetSet + KeyExpire would leave the key without an expiry if
            // the process faulted between the two calls.
            var result = await ObserveWrite(Redis.ScriptEvaluateAsync(
                "local old = redis.call('get', KEYS[1]); redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2]); return old",
                [key], [(RedisValue)value, (RedisValue)(long)expiry.TotalMilliseconds]), cancellationToken);
            return (string?)result;
        }

        public async Task Set(string key, string? value, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            await StringSetAsync(key, value, expiry: expiry, cancellationToken: cancellationToken);
        }

        public async Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            await Set(key, value?.Serialize(), expiry, cancellationToken);
        }

        public async Task<string?> GetAndRefreshExpiry(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            // GETEX in one round trip rather than an awaited GET followed by a fire-and-forget EXPIRE, halving
            // command volume on a sliding-expiration cache hit. A no-op on a missing key, same as ExpireAndForget.
            return await ObserveWrite(Redis.StringGetSetExpiryAsync(key, expiry), cancellationToken);
        }

        public async Task<T?> GetAndRefreshExpiry<T>(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            var val = await GetAndRefreshExpiry(key, expiry, cancellationToken);
            return val.Deserialize<T>();
        }

        public void ExpireAndForget(string key, TimeSpan expiry)
        {
            Redis.KeyExpire(key, expiry, CommandFlags.FireAndForget);
        }

        public void SetAndForget(string key, string? value, TimeSpan expiry)
        {
            Redis.StringSet(key, value, expiry: expiry, flags: CommandFlags.FireAndForget);
        }

        public void SetAndForget<T>(string key, T value, TimeSpan expiry)
        {
            SetAndForget(key, value?.Serialize(), expiry);
        }

        public async Task CompareAndDelete(string key, string deleteIfValue, CancellationToken cancellationToken = default)
        {
            await ObserveWrite(Redis.ScriptEvaluateAsync("if redis.call('get', KEYS[1]) == ARGV[1] then redis.call('del', KEYS[1]) end", [key], [deleteIfValue]), cancellationToken);
        }

        public void ReclaimAndForget(string key, string ownerValue, TimeSpan expiry)
        {
            // "SET NX + expire" in one Lua script: claim the key as ownerValue only if it is currently unset
            // (the resurrection path ExpireAndForget lacks — a bare expire no-ops on a missing key), otherwise
            // just extend the TTL of whatever is already there, mirroring ExpireAndForget's existing
            // don't-care-who-asked refresh so a stale caller still can't overwrite a newer claim's value.
            // CommandFlags.FireAndForget keeps it off the hot inbound-message path the same way ExpireAndForget is.
            Redis.ScriptEvaluate(
                "if redis.call('exists', KEYS[1]) == 0 then redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2]) else redis.call('pexpire', KEYS[1], ARGV[2]) end",
                [key], [(RedisValue)ownerValue, (RedisValue)(long)expiry.TotalMilliseconds],
                flags: CommandFlags.FireAndForget);
        }

        public async Task<bool> CompareAndSet(string key, string? expectedValue, string newValue, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            // Compare-and-set in one Lua script (mirroring CompareAndDelete) so the read and the conditional
            // write are atomic — the read-modify-write a caller layers on top can therefore never lose an update.
            // A null expected asserts the key is still absent; otherwise the swap applies only if the stored
            // value is unchanged. The TTL is written alongside the value so the key never lingers without one.
            var expiryMs = (long)expiry.TotalMilliseconds;
            var result = expectedValue is null
                ? await ObserveWrite(Redis.ScriptEvaluateAsync(
                    "if redis.call('exists', KEYS[1]) == 1 then return 0 end redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2]) return 1",
                    [key], [(RedisValue)newValue, (RedisValue)expiryMs]), cancellationToken)
                : await ObserveWrite(Redis.ScriptEvaluateAsync(
                    "if redis.call('get', KEYS[1]) ~= ARGV[3] then return 0 end redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2]) return 1",
                    [key], [(RedisValue)newValue, (RedisValue)expiryMs, (RedisValue)expectedValue]), cancellationToken);
            return (long)result == 1;
        }

        public async Task Delete(string key, CancellationToken cancellationToken = default)
        {
            await ObserveWrite(Redis.KeyDeleteAsync(key), cancellationToken);
        }

        public void DeleteAndForget(string key)
        {
            Redis.KeyDelete(key, CommandFlags.FireAndForget);
        }

        public async Task<Dictionary<string, string>?> HashGetAllIfExists(string key, CancellationToken cancellationToken = default)
        {
            // TYPE-then-HGETALL in one script (rather than two round trips) so a hot per-battle read pays only
            // one — and so the two checks can't race a concurrent expiry/eviction between them. Lua false
            // comes back over RESP as a null reply, which is how a missing key is told apart from one whose
            // hash happens to have no fields. A key still holding an older, non-hash representation (e.g. a
            // caller that repurposed a string-valued key into a hash-valued one) is treated the same as a miss
            // and cleared, so a stale value self-heals via the normal miss-then-reload path on next write
            // rather than erroring every future read.
            var result = await RedisCommandBudget.Read(Redis.ScriptEvaluateAsync(
                "local t = redis.call('type', KEYS[1]).ok "
                + "if t == 'none' then return false end "
                + "if t ~= 'hash' then redis.call('del', KEYS[1]) return false end "
                + "return redis.call('hgetall', KEYS[1])",
                [key]), cancellationToken);

            if (result.IsNull)
            {
                return null;
            }

            var flat = (RedisValue[])result!;
            var fields = new Dictionary<string, string>(flat.Length / 2);
            for (var i = 0; i < flat.Length; i += 2)
            {
                fields[flat[i]!] = flat[i + 1]!;
            }

            return fields;
        }

        public void HashSetAndForget(string key, IReadOnlyDictionary<string, string> fields, TimeSpan expiry)
        {
            if (fields.Count == 0)
            {
                return;
            }

            // Bundles every field write and the TTL reset into one atomic script (mirroring GetSet/
            // ReclaimAndForget) so the hash is never left holding freshly-written fields without its expiry
            // refreshed.
            Redis.ScriptEvaluate(
                "for i = 2, #ARGV, 2 do redis.call('hset', KEYS[1], ARGV[i], ARGV[i + 1]) end redis.call('pexpire', KEYS[1], ARGV[1])",
                [key], BuildHashArgv(fields, expiry), flags: CommandFlags.FireAndForget);
        }

        public void HashSetIfExistsAndForget(string key, IReadOnlyDictionary<string, string> fields, TimeSpan expiry)
        {
            if (fields.Count == 0)
            {
                return;
            }

            // Same script as HashSetAndForget, guarded by an existence check so a key that vanished (eviction
            // under memory pressure, an operator delete) is never resurrected from a caller's partial field
            // view — resurrecting it would recreate the hash holding only these fields, silently shadowing
            // every other row the caller didn't touch this call.
            Redis.ScriptEvaluate(
                "if redis.call('exists', KEYS[1]) == 1 then "
                + "for i = 2, #ARGV, 2 do redis.call('hset', KEYS[1], ARGV[i], ARGV[i + 1]) end "
                + "redis.call('pexpire', KEYS[1], ARGV[1]) "
                + "end",
                [key], BuildHashArgv(fields, expiry), flags: CommandFlags.FireAndForget);
        }

        // Shared argv layout for both hash-set scripts above: [expiry-ms, then field/value pairs].
        private static RedisValue[] BuildHashArgv(IReadOnlyDictionary<string, string> fields, TimeSpan expiry)
        {
            var argv = new RedisValue[1 + fields.Count * 2];
            argv[0] = (long)expiry.TotalMilliseconds;
            var i = 1;
            foreach (var (field, value) in fields)
            {
                argv[i++] = field;
                argv[i++] = value;
            }

            return argv;
        }

        private async Task StringSetAsync(string key, string? value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None, When when = When.Always, CancellationToken cancellationToken = default)
        {
            await ObserveWrite(Redis.StringSetAsync(key, value, expiry: expiry, flags: flags, when: when), cancellationToken);
        }

        // Awaits a write command under the cancellation budget, attaching a fault-logging continuation so an
        // abandoned write that later faults — a silently failed write with no other signal — is surfaced not lost.
        private Task<T> ObserveWrite<T>(Task<T> command, CancellationToken cancellationToken)
        {
            return RedisCommandBudget.Write(command, cancellationToken, _logger, WriteFaultMessage);
        }
    }
}
