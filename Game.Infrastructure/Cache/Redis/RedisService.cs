using Game.Core;
using Game.Core.Infrastructure;
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

        public string? Get(string key)
        {
            return Redis.StringGet(key);
        }

        public T? Get<T>(string key)
        {
            return Get(key).Deserialize<T>();
        }

        public async Task<string?> GetAsync(string key)
        {
            return await Redis.StringGetAsync(key);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var val = await GetAsync(key);
            return val.Deserialize<T>();
        }

        public string? GetDelete(string key)
        {
            return Redis.StringGetDelete(key);
        }

        public T? GetDelete<T>(string key)
        {
            return GetDelete(key).Deserialize<T>();
        }

        public async Task<string?> GetDeleteAsync(string key)
        {
            return await Redis.StringGetDeleteAsync(key);
        }

        public async Task<T?> GetDeleteAsync<T>(string key)
        {
            var val = await GetDeleteAsync(key);
            return val.Deserialize<T>();
        }

        public string? GetSet(string key, string? value)
        {
            return Redis.StringGetSet(key, value);
        }

        public T? GetSet<T>(string key, T value)
        {
            return GetSet(key, value?.Serialize()).Deserialize<T>();
        }

        public async Task<string?> GetSetAsync(string key, string? value)
        {
            return await Redis.StringGetSetAsync(key, value);
        }

        public async Task<T?> GetSetAsync<T>(string key, T value)
        {
            var val = await GetSetAsync(key, value?.Serialize());
            return val.Deserialize<T>();
        }

        public void Set(string key, string? value)
        {
            StringSet(key, value);
        }

        public void Set<T>(string key, T value)
        {
            Set(key, value?.Serialize());
        }

        public async Task SetAsync(string key, string? value)
        {
            await StringSetAsync(key, value);
        }

        public async Task SetAsync<T>(string key, T value)
        {
            await SetAsync(key, value?.Serialize());
        }

        public void SetAndForget(string key, string? value)
        {
            StringSet(key, value, CommandFlags.FireAndForget);
        }

        public void SetAndForget<T>(string key, T value)
        {
            SetAndForget(key, value?.Serialize());
        }

        public async Task SetAndForgetAsync(string key, string? value)
        {
            await StringSetAsync(key, value, CommandFlags.FireAndForget);
        }

        public async Task SetAndForgetAsync<T>(string key, T value)
        {
            await SetAndForgetAsync(key, value?.Serialize());
        }

        public void SetNotExists(string key, string value)
        {
            StringSet(key, value, when: When.NotExists);
        }

        public async Task SetNotExistsAsync(string key, string value)
        {
            await StringSetAsync(key, value, when: When.NotExists);
        }

        public void CompareAndDelete(string key, string deleteIfValue)
        {
            Redis.ScriptEvaluate("if redis.call('get', KEYS[1]) == ARGV[1] then redis.call('del', KEYS[1]) end", [key], [deleteIfValue]);
        }

        public async Task CompareAndDeleteAsync(string key, string deleteIfValue)
        {
            await Redis.ScriptEvaluateAsync("if redis.call('get', KEYS[1]) == ARGV[1] then redis.call('del', KEYS[1]) end", [key], [deleteIfValue]);
        }

        public void Delete(string key)
        {
            Redis.KeyDelete(key);
        }

        public async Task DeleteAsync(string key)
        {
            await Redis.KeyDeleteAsync(key);
        }

        private void StringSet(string key, string? value, CommandFlags flags = CommandFlags.None, When when = When.Always)
        {
            Redis.StringSet(key, value, flags: flags, when: when);
        }

        private async Task StringSetAsync(string key, string? value, CommandFlags flags = CommandFlags.None, When when = When.Always)
        {
            await Redis.StringSetAsync(key, value, flags: flags, when: when);
        }
    }
}
