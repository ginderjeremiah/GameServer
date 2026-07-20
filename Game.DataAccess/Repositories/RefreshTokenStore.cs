using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Redis-backed implementation of <see cref="IRefreshTokenStore"/>. Refresh tokens are
    /// high-entropy random values; only a SHA-256 hash of the value is used as the cache key, so the
    /// raw secret is never persisted. The cache entry's TTL enforces the token lifetime, and
    /// <see cref="Consume"/> uses an atomic get-and-delete so a token can only be redeemed once
    /// (rotation). This sits in the same Redis instance as the player session store, which auth
    /// already depends on.
    /// </summary>
    internal class RefreshTokenStore : IRefreshTokenStore
    {
        private static string Prefix => Constants.CACHE_REFRESH_TOKEN_PREFIX;

        private readonly ICacheService _cache;

        public RefreshTokenStore(ICacheService cache)
        {
            _cache = cache;
        }

        public async Task<string> Issue(int userId, int? playerId, TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            var token = GenerateToken();
            await _cache.Set(Key(token), new RefreshTokenData(userId, playerId), lifetime, cancellationToken);
            return token;
        }

        public async Task<RefreshTokenData?> Consume(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            return await _cache.GetDelete<RefreshTokenData>(Key(refreshToken), cancellationToken);
        }

        private static string Key(string token)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return $"{Prefix}_{Convert.ToHexString(hash)}";
        }

        private static string GenerateToken()
        {
            // Base64Url emits the URL-safe alphabet without padding, so no manual trim/replace is needed.
            return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        }
    }
}
