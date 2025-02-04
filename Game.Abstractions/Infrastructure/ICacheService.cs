namespace Game.Abstractions.Infrastructure
{
    public interface ICacheService
    {
        public string? Get(string key);
        public T? Get<T>(string key);
        public Task<string?> GetAsync(string key);
        public Task<T?> GetAsync<T>(string key);
        public string? GetDelete(string key);
        public T? GetDelete<T>(string key);
        public Task<string?> GetDeleteAsync(string key);
        public Task<T?> GetDeleteAsync<T>(string key);
        public string? GetSet(string key, string value);
        public T? GetSet<T>(string key, T value);
        public Task<string?> GetSetAsync(string key, string value);
        public Task<T?> GetSetAsync<T>(string key, T value);
        public void Set(string key, string value);
        public void Set<T>(string key, T value);
        public Task SetAsync(string key, string value);
        public Task SetAsync<T>(string key, T value);
        public void SetAndForget(string key, string value);
        public void SetAndForget<T>(string key, T value);
        public Task SetAndForgetAsync(string key, string value);
        public Task SetAndForgetAsync<T>(string key, T value);
        public void SetNotExists(string key, string value);
        public Task SetNotExistsAsync(string key, string value);
        public void Delete(string key);
        public Task DeleteAsync(string key);
        public void CompareAndDelete(string key, string deleteIfValue);
        public Task CompareAndDeleteAsync(string key, string deleteIfValue);
    }
}
