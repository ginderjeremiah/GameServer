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
        /// <summary>
        /// Sets <paramref name="key"/> to <paramref name="value"/> with <paramref name="expiry"/> as its TTL.
        /// A null <paramref name="value"/> deletes the key — the null-means-delete semantic the generic overload
        /// relies on.
        /// </summary>
        public Task Set(string key, string? value, TimeSpan expiry, CancellationToken cancellationToken = default);
        public Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default);
        /// <summary>
        /// Reads <paramref name="key"/> and resets its TTL to <paramref name="expiry"/> in a single round trip
        /// (a no-op on a missing key, like <see cref="ExpireAndForget"/>). Lets a sliding-expiration cache hit
        /// avoid the separate awaited get followed by a fire-and-forget expire.
        /// </summary>
        public Task<string?> GetAndRefreshExpiry(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
        public Task<T?> GetAndRefreshExpiry<T>(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
        /// <summary>
        /// Resets the time-to-live on an existing key without awaiting the result (fire-and-forget).
        /// A no-op if the key does not exist. Lets a hot read path slide a sliding-expiration TTL
        /// without paying a round-trip.
        /// </summary>
        public void ExpireAndForget(string key, TimeSpan expiry);
        /// <summary>
        /// Sets <paramref name="key"/> to <paramref name="value"/> with <paramref name="expiry"/> as its TTL
        /// without awaiting the result (fire-and-forget). A null <paramref name="value"/> deletes the key — the
        /// null-means-delete semantic the generic overload relies on.
        /// </summary>
        public void SetAndForget(string key, string? value, TimeSpan expiry);
        public void SetAndForget<T>(string key, T value, TimeSpan expiry);
        public Task Delete(string key, CancellationToken cancellationToken = default);
        public void DeleteAndForget(string key);
        public Task CompareAndDelete(string key, string deleteIfValue, CancellationToken cancellationToken = default);
        /// <summary>
        /// Atomically sets <paramref name="key"/> to <paramref name="newValue"/> with <paramref name="expiry"/>
        /// as its TTL, but only if its current value still equals <paramref name="expectedValue"/> — or, when
        /// <paramref name="expectedValue"/> is <see langword="null"/>, only if the key is currently unset.
        /// Returns <see langword="true"/> when the swap was applied and <see langword="false"/> when the stored
        /// value had diverged (a concurrent writer won), letting a caller retry its read-modify-write so an
        /// update is never silently lost. The optimistic-concurrency counterpart to <see cref="CompareAndDelete"/>.
        /// </summary>
        public Task<bool> CompareAndSet(string key, string? expectedValue, string newValue, TimeSpan expiry, CancellationToken cancellationToken = default);
        /// <summary>
        /// Fire-and-forget "SET NX + expire": if <paramref name="key"/> is currently unset, claims it as
        /// <paramref name="ownerValue"/> with <paramref name="expiry"/> as its TTL; otherwise just extends the
        /// TTL of whatever value is already there (matching <see cref="ExpireAndForget"/>'s existing
        /// don't-care-who-asked refresh — it never overwrites a value that isn't <paramref name="ownerValue"/>).
        /// Unlike <see cref="ExpireAndForget"/> (a bare TTL bump that no-ops on a missing key), this can
        /// resurrect a claim that expired or was rolled back out from under a still-live owner.
        /// </summary>
        public void ReclaimAndForget(string key, string ownerValue, TimeSpan expiry);
        /// <summary>
        /// Reads every field of the Redis hash at <paramref name="key"/> in one round trip, or
        /// <see langword="null"/> if the key does not exist — letting a caller distinguish a genuine cache
        /// miss from a hash that exists but happens to carry no fields.
        /// </summary>
        public Task<Dictionary<string, string>?> HashGetAllIfExists(string key, CancellationToken cancellationToken = default);
        /// <summary>
        /// Same as <see cref="HashGetAllIfExists"/>, but also resets the key's TTL to <paramref name="expiry"/>
        /// in the same round trip on a hit (a no-op on a miss, like <see cref="GetAndRefreshExpiry"/>). Lets a
        /// sliding-expiration hash read avoid the separate awaited HGETALL followed by a fire-and-forget expire.
        /// </summary>
        public Task<Dictionary<string, string>?> HashGetAllAndRefreshExpiry(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
        /// <summary>
        /// Atomically writes <paramref name="fields"/> into the Redis hash at <paramref name="key"/> (adding
        /// or overwriting each named field; existing fields not named in <paramref name="fields"/> are left
        /// untouched) and resets the key's TTL to <paramref name="expiry"/>, without awaiting the result
        /// (fire-and-forget). A no-op when <paramref name="fields"/> is empty — an empty HSET call would
        /// error, and there is nothing whose TTL needs refreshing. Lets a hot write path persist only the
        /// rows that changed instead of re-serializing an entire aggregate.
        /// </summary>
        public void HashSetAndForget(string key, IReadOnlyDictionary<string, string> fields, TimeSpan expiry);
        /// <summary>
        /// Same as <see cref="HashSetAndForget"/>, but a no-op (fields untouched, TTL untouched) if
        /// <paramref name="key"/> does not currently exist. Lets a caller that only ever holds a *partial*
        /// view of the hash (e.g. this save's dirty rows) advance an existing cache entry without resurrecting
        /// one that was evicted or deleted out from under it — creating a hash from a partial view would leave
        /// it silently missing every row the caller didn't touch this call.
        /// </summary>
        public void HashSetIfExistsAndForget(string key, IReadOnlyDictionary<string, string> fields, TimeSpan expiry);
    }
}
