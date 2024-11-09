using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Core.Infrastructure;
using Game.Infrastructure.Database;

namespace Game.DataAccess.Repositories
{
    internal class SessionStore : ISessionStore
    {
        private static string SessionPrefix => Constants.CACHE_SESSION_PREFIX;

        private readonly GameContext _context;
        private readonly ICacheService _cache;

        public SessionStore(GameContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<SessionData?> GetSession(int playerId)
        {
            return await _cache.GetAsync<SessionData>($"{SessionPrefix}_{playerId}");
        }

        public SessionData GetNewSessionData(int playerId)
        {
            var sessionData = new SessionData(Guid.NewGuid().ToString())
            {
                LastUsed = DateTime.UtcNow,
                CurrentZone = 0,
                EnemyCooldown = DateTime.UnixEpoch,
                EarliestDefeat = DateTime.UnixEpoch,
                Victory = false,
                PlayerId = playerId
            };

            _cache.SetAndForget($"{SessionPrefix}_{sessionData.PlayerId}", sessionData);
            return sessionData;
        }

        public void Update(SessionData sessionData)
        {
            _cache.SetAndForget($"{SessionPrefix}_{sessionData.PlayerId}", sessionData);
        }

        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash)
        {
            _cache.SetAndForget($"{Constants.CACHE_ACTIVE_ENEMY_PREFIX}_{sessionData.PlayerId}", activeEnemyHash);
        }

        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData)
        {
            return _cache.GetDelete($"{Constants.CACHE_ACTIVE_ENEMY_PREFIX}_{sessionData.PlayerId}");
        }
    }
}
