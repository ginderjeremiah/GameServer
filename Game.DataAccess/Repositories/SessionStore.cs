using Game.Abstractions.Auth;
using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core.Players;


namespace Game.DataAccess.Repositories
{
    // Sessions are keyed by the user/account id (single active session per user).
    // Note PlayerState.PlayerId is a distinct value and is not used as the key.
    internal class SessionStore : ISessionStore
    {
        private static string SessionPrefix => Constants.CACHE_SESSION_PREFIX;

        /// <summary>
        /// Idle TTL for the cached session (<c>Session_{userId}</c> → the in-flight <see cref="PlayerState"/>).
        /// Written on every <see cref="Update"/> and slid on every <see cref="GetSession"/> hit, mirroring the
        /// player-aggregate eviction policy (#439) so an active session never ages out while a dormant one is
        /// reclaimed instead of occupying Redis forever (#537). The user→player binding is reconstructable
        /// (a miss is rehydrated in-memory by <c>SessionInitializer</c> from the token's selected-player claim,
        /// so it is never a silent logout — authentication derives from the token, not this cache), but the in-flight battle snapshot
        /// it also carries is cache-only and lost on eviction. The budget is therefore the refresh-token
        /// lifetime as a genuine floor: an active player's in-flight battle should not be dropped while their
        /// refresh token is still valid (see docs/backend-persistence.md → Caching and Pub/Sub).
        /// </summary>
        private static readonly TimeSpan SessionCacheTtl = AuthConstants.RefreshTokenLifetime;

        private readonly ICacheService _cache;

        public SessionStore(ICacheService cache)
        {
            _cache = cache;
        }

        public async Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default)
        {
            var sessionKey = SessionKey(userId);
            var session = await _cache.Get<PlayerState>(sessionKey, cancellationToken);
            if (session is not null)
            {
                // Sliding expiration: a read refreshes the idle TTL so an active session never ages out.
                _cache.ExpireAndForget(sessionKey, SessionCacheTtl);
            }

            return session;
        }

        public void Update(PlayerState playerState, int userId)
        {
            _cache.SetAndForget(SessionKey(userId), playerState, SessionCacheTtl);
        }

        public async Task UpdateAsync(PlayerState playerState, int userId, CancellationToken cancellationToken = default)
        {
            await _cache.Set(SessionKey(userId), playerState, SessionCacheTtl, cancellationToken);
        }

        public void Clear(int userId)
        {
            _cache.DeleteAndForget(SessionKey(userId));
        }

        private static string SessionKey(int userId) => $"{SessionPrefix}_{userId}";
    }
}
