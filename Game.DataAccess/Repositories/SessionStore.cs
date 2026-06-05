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

        public async Task<PlayerState?> GetSession(int userId)
        {
            return await _cache.Get<PlayerState>($"{SessionPrefix}_{userId}");
        }

        public void Update(PlayerState playerState, int playerId)
        {
            _cache.SetAndForget($"{SessionPrefix}_{playerId}", playerState);
        }

        public void Clear(int userId)
        {
            _cache.DeleteAndForget($"{SessionPrefix}_{userId}");
        }
    }
}
