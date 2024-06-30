﻿using GameCore;
using GameCore.Infrastructure;
using StackExchange.Redis;

namespace GameInfrastructure.Cache.Redis
{
    internal class RedisService : ICacheService
    {
        private ConnectionMultiplexer Multiplexer { get; }
        public IDatabase Redis => Multiplexer.GetDatabase();

        public RedisService(ConnectionMultiplexer multiplexer)
        {
            Multiplexer = multiplexer;
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