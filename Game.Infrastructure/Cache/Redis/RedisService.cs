using Game.Abstractions.Infrastructure;
using Game.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Game.Infrastructure.Cache.Redis
{
    internal class RedisService : ICacheService
    {
        private ConnectionMultiplexer Multiplexer { get; }
        private readonly ILogger<RedisService> _logger;
        public IDatabase Redis => Multiplexer.GetDatabase();

        public RedisService(ConnectionMultiplexer multiplexer, ILogger<RedisService> logger)
        {
            Multiplexer = multiplexer;
            _logger = logger;
        }

        // StackExchange.Redis exposes no CancellationToken on its database operations, so the token is honoured
        // only partially: WaitAsync makes the *await* unwind promptly when the budget is cancelled (releasing the
        // per-socket command lock without waiting out the dependency's own 5s timeout — #558), while the
        // underlying command keeps running to completion in the background. WaitAsync(CancellationToken.None) is a
        // zero-overhead no-op (it returns the same task), so the default-token callers pay nothing.
        //
        // For write operations the abandoned command's eventual fault would otherwise go unobserved — a silently
        // failed write with no signal — so every mutating call (including the read-modify-write GetSet/GetDelete)
        // routes through ObserveWrite, which attaches a fault-logging continuation when (and only when) the await
        // is cancelled. Pure reads (Get) are exempt: a post-cancellation fault there loses only an unobserved
        // read, not a write.

        public async Task<string?> Get(string key, CancellationToken cancellationToken = default)
        {
            // Honour an already-cancelled budget before issuing the read: WaitAsync alone is racy because it
            // returns the inner task unchanged when that task is already complete (it checks IsCompleted before
            // the token), so a command that finished first would silently swallow the cancellation.
            cancellationToken.ThrowIfCancellationRequested();
            return await Redis.StringGetAsync(key).WaitAsync(cancellationToken);
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

        public async Task<string?> GetSet(string key, string? value, CancellationToken cancellationToken = default)
        {
            // Read-and-set is a write (it stores the new value), so it routes through ObserveWrite too.
            return await ObserveWrite(Redis.StringGetSetAsync(key, value), cancellationToken);
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
            var result = await ObserveWrite(Redis.ScriptEvaluateAsync(
                "local old = redis.call('get', KEYS[1]); redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2]); return old",
                [key], [(RedisValue)value, (RedisValue)(long)expiry.TotalMilliseconds]), cancellationToken);
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
            await ObserveWrite(Redis.KeyExpireAsync(key, expiry), cancellationToken);
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
            await ObserveWrite(Redis.ScriptEvaluateAsync("if redis.call('get', KEYS[1]) == ARGV[1] then redis.call('del', KEYS[1]) end", [key], [deleteIfValue]), cancellationToken);
        }

        public async Task Delete(string key, CancellationToken cancellationToken = default)
        {
            await ObserveWrite(Redis.KeyDeleteAsync(key), cancellationToken);
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
            await ObserveWrite(Redis.StringSetAsync(key, value, expiry: expiry, flags: flags, when: when), cancellationToken);
        }

        // Awaits a write command under the cancellation budget. On a non-cancelled await a fault surfaces here
        // directly; on cancellation the await unwinds but the underlying command keeps running (StackExchange.Redis
        // can't cancel it), so a fault-logging continuation is attached to the abandoned task before rethrowing —
        // the write may have silently failed, and the next save self-heals the value but would otherwise leave no
        // signal. ExecuteSynchronously + OnlyOnFaulted keeps it allocation-light and silent on success.
        private async Task<T> ObserveWrite<T>(Task<T> command, CancellationToken cancellationToken)
        {
            try
            {
                return await command.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _ = command.ContinueWith(
                    faulted => _logger.LogError(faulted.Exception, "A Redis write faulted after its command budget was cancelled; the write may not have been applied."),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                throw;
            }
        }
    }
}
