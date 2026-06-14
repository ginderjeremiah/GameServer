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

        private readonly ICacheService _cache;

        public SessionStore(ICacheService cache)
        {
            _cache = cache;
        }

        public async Task<PlayerState?> GetSession(int userId)
        {
            return await _cache.Get<PlayerState>($"{SessionPrefix}_{userId}");
        }

        public void Update(PlayerState playerState, int userId)
        {
            _cache.SetAndForget($"{SessionPrefix}_{userId}", playerState);
        }

        public void Clear(int userId)
        {
            _cache.DeleteAndForget($"{SessionPrefix}_{userId}");
        }
    }
}
