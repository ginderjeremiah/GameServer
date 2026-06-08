using Game.Abstractions.Infrastructure;
using Game.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Game.Infrastructure.Cache.Redis
{
    internal class RedisService : ICacheService
    {
        private readonly ILogger<RedisService> _logger;
        private ConnectionMultiplexer Multiplexer { get; }
        public IDatabase Redis => Multiplexer.GetDatabase();

        public RedisService(ConnectionMultiplexer multiplexer, ILogger<RedisService> logger)
        {
            Multiplexer = multiplexer;
            _logger = logger;
        }

        public async Task<string?> Get(string key)
        {
            return await Redis.StringGetAsync(key);
        }

        public async Task<T?> Get<T>(string key)
        {
            var val = await Get(key);
            return val.Deserialize<T>();
        }

        public async Task<string?> GetDelete(string key)
        {
            return await Redis.StringGetDeleteAsync(key);
        }

        public async Task<T?> GetDelete<T>(string key)
        {
            var val = await GetDelete(key);
            return val.Deserialize<T>();
        }

        public async Task<string?> GetSet(string key, string? value)
        {
            return await Redis.StringGetSetAsync(key, value);
        }

        public async Task<T?> GetSet<T>(string key, T value)
        {
            var val = await GetSet(key, value?.Serialize());
            return val.Deserialize<T>();
        }

        public async Task Set(string key, string? value)
        {
            await StringSetAsync(key, value);
        }

        public async Task Set<T>(string key, T value)
        {
            await Set(key, value?.Serialize());
        }

        public async Task Set(string key, string? value, TimeSpan expiry)
        {
            await StringSetAsync(key, value, expiry: expiry);
        }

        public async Task Set<T>(string key, T value, TimeSpan expiry)
        {
            await Set(key, value?.Serialize(), expiry);
        }

        public async Task Expire(string key, TimeSpan expiry)
        {
            await Redis.KeyExpireAsync(key, expiry);
        }

        public void SetAndForget(string key, string? value)
        {
            StringSet(key, value, CommandFlags.FireAndForget);
        }

        public void SetAndForget<T>(string key, T value)
        {
            SetAndForget(key, value?.Serialize());
        }

        public async Task SetNotExists(string key, string value)
        {
            await StringSetAsync(key, value, when: When.NotExists);
        }

        public async Task CompareAndDelete(string key, string deleteIfValue)
        {
            await Redis.ScriptEvaluateAsync("if redis.call('get', KEYS[1]) == ARGV[1] then redis.call('del', KEYS[1]) end", [key], [deleteIfValue]);
        }

        public async Task Delete(string key)
        {
            await Redis.KeyDeleteAsync(key);
        }

        public void DeleteAndForget(string key)
        {
            Redis.KeyDelete(key, CommandFlags.FireAndForget);
        }

        private void StringSet(string key, string? value, CommandFlags flags = CommandFlags.None, When when = When.Always)
        {
            Redis.StringSet(key, value, flags: flags, when: when);
        }

        private async Task StringSetAsync(string key, string? value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None, When when = When.Always)
        {
            await Redis.StringSetAsync(key, value, expiry: expiry, flags: flags, when: when);
        }
    }
}
