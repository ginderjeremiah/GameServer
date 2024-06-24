using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class SessionStore : BaseRepository, ISessionStore
    {
        private readonly ICacheService _cache;
        private readonly DataProviderSynchronizer _synchronizer;
        private static string SessionPrefix => Constants.CACHE_SESSION_PREFIX;
        public SessionStore(IDatabaseService database, ICacheService cache, DataProviderSynchronizer synchronizer) : base(database)
        {
            _cache = cache;
            _synchronizer = synchronizer;
        }

        public async Task<SessionData?> GetSessionAsync(string id)
        {
            return await _cache.GetAsync<SessionData>($"{SessionPrefix}_{id}");
        }

        public async Task<SessionData> GetNewSessionDataAsync(int playerId)
        {
            var player = await Database.Players
                .Include(p => p.InventoryItems.Select(i => i.Item.ItemAttributes))
                .Include(p => p.InventoryItems.Select(i => i.InventoryItemMods.Select(im => im.ItemMod.ItemModAttributes)))
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                .FirstOrDefaultAsync() ?? throw new ArgumentOutOfRangeException(nameof(playerId));

            var sessionData = new SessionData(Guid.NewGuid().ToString())
            {
                LastUsed = DateTime.UtcNow,
                CurrentZone = 0,
                PlayerData = player,
                EnemyCooldown = DateTime.UnixEpoch,
                EarliestDefeat = DateTime.UnixEpoch,
                Victory = false,
            };

            _cache.SetAndForget($"{SessionPrefix}_{sessionData.Id}", sessionData);
            return sessionData;
        }

        public async Task UpdateAsync(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty)
        {
            _cache.SetAndForget($"{SessionPrefix}_{sessionData.Id}", sessionData);
            if (inventoryDirty)
            {
                _synchronizer.SynchronizeInventory(sessionData.Id);
            }
            if (playerDirty)
            {
                _synchronizer.SynchronizePlayerData(sessionData.Id);
            }
            if (skillsDirty)
            {
                _synchronizer.SynchronizeSkills(sessionData.Id);
            }

            await Database.SaveChangesAsync();
        }

        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash)
        {
            _cache.SetAndForget($"{Constants.CACHE_ACTIVE_ENEMY_PREFIX}_{sessionData.Id}", activeEnemyHash);
        }

        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData)
        {
            return _cache.GetDelete($"{Constants.CACHE_ACTIVE_ENEMY_PREFIX}_{sessionData.Id}");
        }
    }
}
