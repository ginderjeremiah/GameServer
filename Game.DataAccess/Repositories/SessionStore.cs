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

        public void SetBattleDataHash(int playerId, string activeEnemyHash)
        {
            _cache.SetAndForget($"{Constants.CACHE_BATTLE_DATA_PREFIX}_{playerId}", activeEnemyHash);
        }

        public async Task<string?> GetAndDeleteBattleDataHash(int playerId)
        {
            return await _cache.GetDeleteAsync($"{Constants.CACHE_BATTLE_DATA_PREFIX}_{playerId}");
        }
    }
}
