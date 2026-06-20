using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using System.Security.Cryptography;
using System.Text;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Redis-backed implementation of <see cref="ILoginBackoffStore"/>. The account is keyed by a SHA-256
    /// hash of the (case-sensitive) username — the exact-match semantics login itself uses — so the raw
    /// username is not stored as a key and the key length is fixed regardless of the supplied value. The
    /// entry's TTL self-expires a stale failure streak. This sits in the same Redis instance as the refresh
    /// token store, which auth already depends on.
    /// </summary>
    internal class LoginBackoffStore : ILoginBackoffStore
    {
        private static string Prefix => Constants.CACHE_LOGIN_BACKOFF_PREFIX;

        private readonly ICacheService _cache;

        public LoginBackoffStore(ICacheService cache)
        {
            _cache = cache;
        }

        public Task<LoginBackoffState?> Get(string username, CancellationToken cancellationToken = default)
        {
            return _cache.Get<LoginBackoffState>(Key(username), cancellationToken);
        }

        public Task Set(string username, LoginBackoffState state, TimeSpan retention, CancellationToken cancellationToken = default)
        {
            return _cache.Set(Key(username), state, retention, cancellationToken);
        }

        public Task Clear(string username, CancellationToken cancellationToken = default)
        {
            return _cache.Delete(Key(username), cancellationToken);
        }

        private static string Key(string username)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(username));
            return $"{Prefix}_{Convert.ToHexString(hash)}";
        }
    }
}
