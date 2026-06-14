using Game.Abstractions.Infrastructure;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Wraps a real <see cref="ICacheService"/> but makes the awaited <c>Set</c> overloads throw, standing in
    /// for a dropped/failed source-of-truth cache write (a multiplexer hiccup). Every other operation —
    /// including the fire-and-forget <see cref="ICacheService.SetAndForget{T}(string, T)"/> /
    /// <see cref="ICacheService.ExpireAndForget"/> used on the read path — delegates to the inner cache, so a
    /// repository can still load and serve normally; only the awaited save-path write fails. Lets a test prove
    /// that the source-of-truth write surfaces a failure (#580) instead of swallowing it.
    /// </summary>
    public sealed class ThrowingOnSetCacheService(ICacheService inner) : ICacheService
    {
        private readonly ICacheService _inner = inner;

        public Task Set(string key, string value) => throw new CacheWriteFailedException();
        public Task Set<T>(string key, T value) => throw new CacheWriteFailedException();
        public Task Set(string key, string value, TimeSpan expiry) => throw new CacheWriteFailedException();
        public Task Set<T>(string key, T value, TimeSpan expiry) => throw new CacheWriteFailedException();

        public Task<string?> Get(string key) => _inner.Get(key);
        public Task<T?> Get<T>(string key) => _inner.Get<T>(key);
        public Task<string?> GetDelete(string key) => _inner.GetDelete(key);
        public Task<T?> GetDelete<T>(string key) => _inner.GetDelete<T>(key);
        public Task<string?> GetSet(string key, string value) => _inner.GetSet(key, value);
        public Task<T?> GetSet<T>(string key, T value) => _inner.GetSet(key, value);
        public Task Expire(string key, TimeSpan expiry) => _inner.Expire(key, expiry);
        public void ExpireAndForget(string key, TimeSpan expiry) => _inner.ExpireAndForget(key, expiry);
        public void SetAndForget(string key, string value) => _inner.SetAndForget(key, value);
        public void SetAndForget<T>(string key, T value) => _inner.SetAndForget(key, value);
        public void SetAndForget(string key, string value, TimeSpan expiry) => _inner.SetAndForget(key, value, expiry);
        public void SetAndForget<T>(string key, T value, TimeSpan expiry) => _inner.SetAndForget(key, value, expiry);
        public Task SetNotExists(string key, string value) => _inner.SetNotExists(key, value);
        public Task Delete(string key) => _inner.Delete(key);
        public void DeleteAndForget(string key) => _inner.DeleteAndForget(key);
        public Task CompareAndDelete(string key, string deleteIfValue) => _inner.CompareAndDelete(key, deleteIfValue);
    }

    /// <summary>Marker exception thrown by <see cref="ThrowingOnSetCacheService"/> to simulate a failed cache write.</summary>
    public sealed class CacheWriteFailedException() : Exception("Simulated cache write failure.");
}
