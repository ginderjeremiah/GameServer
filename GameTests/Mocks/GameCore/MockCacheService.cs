using GameCore;
using GameCore.Infrastructure;
using System.Diagnostics.CodeAnalysis;

namespace GameTests.Mocks.GameCore
{
    internal class MockCacheService : ICacheService
    {
        public Dictionary<string, string> Cache { get; set; } = new();

        public string? Get(string key)
        {
            Cache.TryGetValue(key, out var value);
            return value;
        }

        public T? Get<T>(string key)
        {
            return Get(key).Deserialize<T>();
        }

        public Task<string?> GetAsync(string key)
        {
            return Task.FromResult(Get(key));
        }

        public Task<T?> GetAsync<T>(string key)
        {
            return Task.FromResult(Get<T>(key));
        }

        public string? GetDelete(string key)
        {
            var val = Get(key);
            if (val == null)
            {
                Cache.Remove(key);
            }
            return val;
        }

        public T? GetDelete<T>(string key)
        {
            return GetDelete(key).Deserialize<T>();
        }

        public Task<string?> GetDeleteAsync(string key)
        {
            return Task.FromResult(GetDelete(key));
        }

        public Task<T?> GetDeleteAsync<T>(string key)
        {
            return Task.FromResult(GetDelete<T>(key));
        }

        public void Set(string key, string value)
        {
            Cache[key] = value;
        }

        public void Set<T>(string key, T value)
        {
            Set(key, value.Serialize());
        }

        public void SetAndForget(string key, string value)
        {
            Set(key, value);
        }

        public void SetAndForget<T>(string key, T value)
        {
            Set(key, value);
        }

        public Task SetAndForgetAsync(string key, string value)
        {
            Set(key, value);
            return Task.CompletedTask;
        }

        public Task SetAndForgetAsync<T>(string key, T value)
        {
            Set(key, value);
            return Task.CompletedTask;
        }

        public Task SetAsync(string key, string value)
        {
            Set(key, value);
            return Task.CompletedTask;
        }

        public Task SetAsync<T>(string key, T value)
        {
            Set(key, value);
            return Task.CompletedTask;
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
    }
}
