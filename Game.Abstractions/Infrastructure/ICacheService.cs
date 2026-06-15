namespace Game.Abstractions.Infrastructure
{
    // The awaiting async members take an optional CancellationToken (defaulting to none, so existing callers
    // are unaffected) so a cancelled per-command budget unwinds cooperatively instead of relying solely on the
    // dependency's own timeout (#558). The fire-and-forget (void) members take none: they return before the
    // command settles, so there is nothing to cancel. StackExchange.Redis does not accept a token on its
    // database operations, so the implementation honours it only partially — see RedisService.
    public interface ICacheService
    {
        public Task<string?> Get(string key, CancellationToken cancellationToken = default);
        public Task<T?> Get<T>(string key, CancellationToken cancellationToken = default);
        public Task<string?> GetDelete(string key, CancellationToken cancellationToken = default);
        public Task<T?> GetDelete<T>(string key, CancellationToken cancellationToken = default);
        public Task<string?> GetSet(string key, string value, CancellationToken cancellationToken = default);
        public Task<T?> GetSet<T>(string key, T value, CancellationToken cancellationToken = default);
        /// <summary>
        /// Atomically sets <paramref name="key"/> to <paramref name="value"/> with <paramref name="expiry"/>
        /// as its TTL and returns the previous value (null if the key was unset). Unlike a separate
        /// <see cref="GetSet(string, string, CancellationToken)"/> followed by
        /// <see cref="Expire(string, TimeSpan, CancellationToken)"/>, the value and its expiry are written in a
        /// single operation, so a fault between the two can never leave the key lingering without a TTL.
        /// </summary>
        public Task<string?> GetSet(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default);
        public Task Set(string key, string value, CancellationToken cancellationToken = default);
        public Task Set<T>(string key, T value, CancellationToken cancellationToken = default);
        public Task Set(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default);
        public Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default);
        /// <summary>Resets the time-to-live on an existing key. A no-op if the key does not exist.</summary>
        public Task Expire(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
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
        public Task SetNotExists(string key, string value, CancellationToken cancellationToken = default);
        public Task Delete(string key, CancellationToken cancellationToken = default);
        public void DeleteAndForget(string key);
        public Task CompareAndDelete(string key, string deleteIfValue, CancellationToken cancellationToken = default);
    }
}
