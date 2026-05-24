using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core.Players;
using Game.Infrastructure.Database;


namespace Game.DataAccess.Repositories
{
    internal class SessionStore : ISessionStore
    {
        private static string SessionPrefix => Constants.CACHE_SESSION_PREFIX;

        private readonly ICacheService _cache;

        public SessionStore(GameContext context, ICacheService cache)
        {
            _cache = cache;
        }

        public async Task<PlayerState?> GetSession(string sessionId)
        {
            return await _cache.GetAsync<PlayerState>($"{SessionPrefix}_{sessionId}");
        }

        public void Update(PlayerState playerState, int playerId)
        {
            _cache.SetAndForget($"{SessionPrefix}_{playerId}", playerState);
        }

    }
}
