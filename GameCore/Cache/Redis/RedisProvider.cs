using GameCore.Redis;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

namespace GameCore.Cache.Redis
{
    internal class RedisProvider : ICacheProvider
    {
        private ConnectionMultiplexer Multiplexer { get; }
        public IDatabase Redis => Multiplexer.GetDatabase();

        public RedisProvider(RedisMultiplexerFactory factory)
        {
            Multiplexer = factory.GetMultiplexer();
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

        public bool TryGet(string key, [NotNullWhen(true)] out string? result)
        {
            result = Get(key);
            return result is not null;
        }

        public bool TryGet<T>(string key, [NotNullWhen(true)] out T? result)
        {
            result = Get<T>(key);
            return result is not null;
        }

        public void Set(string key, string value)
        {
            StringSet(key, value);
        }

        public void Set<T>(string key, T value)
        {
            Set(key, value.Serialize());
        }

        public Task SetAsync(string key, string value)
        {
            return StringSetAsync(key, value);
        }

        public Task SetAsync<T>(string key, T value)
        {
            return SetAsync(key, value.Serialize());
        }

        public void SetAndForget(string key, string value)
        {
            StringSet(key, value, CommandFlags.FireAndForget);
        }

        public void SetAndForget<T>(string key, T value)
        {
            SetAndForget(key, value.Serialize());
        }

        public Task SetAndForgetAsync(string key, string value)
        {
            return StringSetAsync(key, value, CommandFlags.FireAndForget);
        }

        public Task SetAndForgetAsync<T>(string key, T value)
        {
            return SetAndForgetAsync(key, value.Serialize());
        }

        private void StringSet(string key, string value, CommandFlags flags = CommandFlags.None)
        {
            Redis.StringSet(key, value, flags: flags);
        }

        private async Task StringSetAsync(string key, string value, CommandFlags flags = CommandFlags.None)
        {
            await Redis.StringSetAsync(key, value, flags: flags);
        }
    }
}
