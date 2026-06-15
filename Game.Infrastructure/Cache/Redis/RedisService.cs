using Game.Abstractions.Infrastructure;
using Game.Core;
using StackExchange.Redis;

namespace Game.Infrastructure.Cache.Redis
{
    internal class RedisService : ICacheService
    {
        private ConnectionMultiplexer Multiplexer { get; }
        public IDatabase Redis => Multiplexer.GetDatabase();

        public RedisService(ConnectionMultiplexer multiplexer)
        {
            Multiplexer = multiplexer;
        }

        // StackExchange.Redis exposes no CancellationToken on its database operations, so the token is honoured
        // only partially: WaitAsync makes the *await* unwind promptly when the budget is cancelled (releasing the
        // per-socket command lock without waiting out the dependency's own 5s timeout — #558), while the
        // underlying command keeps running to completion in the background. WaitAsync(CancellationToken.None) is a
        // zero-overhead no-op (it returns the same task), so the default-token callers pay nothing.

        public async Task<string?> Get(string key, CancellationToken cancellationToken = default)
        {
            return await Redis.StringGetAsync(key).WaitAsync(cancellationToken);
        }

        public async Task<T?> Get<T>(string key, CancellationToken cancellationToken = default)
        {
            var val = await Get(key, cancellationToken);
            return val.Deserialize<T>();
        }

        public async Task<string?> GetDelete(string key, CancellationToken cancellationToken = default)
        {
            return await Redis.StringGetDeleteAsync(key).WaitAsync(cancellationToken);
        }

        public async Task<T?> GetDelete<T>(string key, CancellationToken cancellationToken = default)
        {
            var val = await GetDelete(key, cancellationToken);
            return val.Deserialize<T>();
        }

        public async Task<string?> GetSet(string key, string? value, CancellationToken cancellationToken = default)
        {
            return await Redis.StringGetSetAsync(key, value).WaitAsync(cancellationToken);
        }

        public async Task<T?> GetSet<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            var val = await GetSet(key, value?.Serialize(), cancellationToken);
            return val.Deserialize<T>();
        }

        public async Task<string?> GetSet(string key, string? value, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            // Read-old-then-write in one Lua script (atomic, mirroring CompareAndDelete) so the value and its
            // TTL land together — a separate StringGetSet + KeyExpire would leave the key without an expiry if
            // the process faulted between the two calls.
            var result = await Redis.ScriptEvaluateAsync(
                "local old = redis.call('get', KEYS[1]); redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2]); return old",
                [key], [(RedisValue)value, (RedisValue)(long)expiry.TotalMilliseconds]).WaitAsync(cancellationToken);
            return (string?)result;
        }

        public async Task Set(string key, string? value, CancellationToken cancellationToken = default)
        {
            await StringSetAsync(key, value, cancellationToken: cancellationToken);
        }

        public async Task Set<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            await Set(key, value?.Serialize(), cancellationToken);
        }

        public async Task Set(string key, string? value, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            await StringSetAsync(key, value, expiry: expiry, cancellationToken: cancellationToken);
        }

        public async Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            await Set(key, value?.Serialize(), expiry, cancellationToken);
        }

        public async Task Expire(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            await Redis.KeyExpireAsync(key, expiry).WaitAsync(cancellationToken);
        }

        public void ExpireAndForget(string key, TimeSpan expiry)
        {
            Redis.KeyExpire(key, expiry, CommandFlags.FireAndForget);
        }

        public void SetAndForget(string key, string? value)
        {
            StringSet(key, value, flags: CommandFlags.FireAndForget);
        }

        public void SetAndForget<T>(string key, T value)
        {
            SetAndForget(key, value?.Serialize());
        }

        public void SetAndForget(string key, string? value, TimeSpan expiry)
        {
            StringSet(key, value, expiry: expiry, flags: CommandFlags.FireAndForget);
        }

        public void SetAndForget<T>(string key, T value, TimeSpan expiry)
        {
            SetAndForget(key, value?.Serialize(), expiry);
        }

        public async Task SetNotExists(string key, string value, CancellationToken cancellationToken = default)
        {
            await StringSetAsync(key, value, when: When.NotExists, cancellationToken: cancellationToken);
        }

        public async Task CompareAndDelete(string key, string deleteIfValue, CancellationToken cancellationToken = default)
        {
            await Redis.ScriptEvaluateAsync("if redis.call('get', KEYS[1]) == ARGV[1] then redis.call('del', KEYS[1]) end", [key], [deleteIfValue]).WaitAsync(cancellationToken);
        }

        public async Task Delete(string key, CancellationToken cancellationToken = default)
        {
            await Redis.KeyDeleteAsync(key).WaitAsync(cancellationToken);
        }

        public void DeleteAndForget(string key)
        {
            Redis.KeyDelete(key, CommandFlags.FireAndForget);
        }

        private void StringSet(string key, string? value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None, When when = When.Always)
        {
            Redis.StringSet(key, value, expiry: expiry, flags: flags, when: when);
        }

        private async Task StringSetAsync(string key, string? value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None, When when = When.Always, CancellationToken cancellationToken = default)
        {
            await Redis.StringSetAsync(key, value, expiry: expiry, flags: flags, when: when).WaitAsync(cancellationToken);
        }
    }
}
