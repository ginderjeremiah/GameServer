namespace Game.Abstractions.Infrastructure
{
    public interface ICacheService
    {
        public Task<string?> Get(string key);
        public Task<T?> Get<T>(string key);
        public Task<string?> GetDelete(string key);
        public Task<T?> GetDelete<T>(string key);
        public Task<string?> GetSet(string key, string value);
        public Task<T?> GetSet<T>(string key, T value);
        public Task Set(string key, string value);
        public Task Set<T>(string key, T value);
        public Task Set(string key, string value, TimeSpan expiry);
        public Task Set<T>(string key, T value, TimeSpan expiry);
        /// <summary>Resets the time-to-live on an existing key. A no-op if the key does not exist.</summary>
        public Task Expire(string key, TimeSpan expiry);
        /// <summary>
        /// Resets the time-to-live on an existing key without awaiting the result (fire-and-forget).
        /// A no-op if the key does not exist. Lets a hot read path slide a sliding-expiration TTL
        /// without paying a round-trip.
        /// </summary>
        public void ExpireAndForget(string key, TimeSpan expiry);
        public void SetAndForget(string key, string value);
        public void SetAndForget<T>(string key, T value);
        public void SetAndForget(string key, string value, TimeSpan expiry);
        public void SetAndForget<T>(string key, T value, TimeSpan expiry);
        public Task SetNotExists(string key, string value);
        public Task Delete(string key);
        public void DeleteAndForget(string key);
        public Task CompareAndDelete(string key, string deleteIfValue);
    }
}
