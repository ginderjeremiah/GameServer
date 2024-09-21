using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using GameInfrastructure.Database;

namespace DataAccess.Repositories
{
    internal class SessionStore : BaseRepository, ISessionStore
    {
        private readonly ICacheService _cache;
        private static string SessionPrefix => Constants.CACHE_SESSION_PREFIX;
        public SessionStore(GameContext database, ICacheService cache) : base(database)
        {
            _cache = cache;
        }

        public async Task<SessionData?> GetSessionAsync(int playerId)
        {
            return await _cache.GetAsync<SessionData>($"{SessionPrefix}_{playerId}");
        }

        public async Task<SessionData> GetNewSessionDataAsync(int playerId)
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
