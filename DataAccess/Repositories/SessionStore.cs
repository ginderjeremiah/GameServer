using GameCore.DataAccess;
using GameCore.Entities.InventoryItems;
using GameCore.Entities.PlayerAttributes;
using GameCore.Entities.Players;
using GameCore.Entities.SessionStore;
using GameCore.Infrastructure;
using System.Diagnostics.CodeAnalysis;

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

        public bool TryGetSession(string id, [NotNullWhen(true)] out SessionData? session)
        {
            return _cache.TryGet($"{SessionPrefix}_{id}", out session);
        }

        public SessionData GetNewSessionData(int playerId)
        {
            string commandText = @"
                SELECT
                    PlayerId,
                    PlayerName,
                    UserName,
                    Level,
                    Salt,
                    PassHash,
	                Level,
	                Exp,
	                StatPointsGained,
	                StatPointsUsed
                FROM Players
                WHERE PlayerId = @PlayerId

                SELECT
                    PlayerId,
                    AttributeId,
                    Amount
                FROM PlayerAttributes
                WHERE PlayerId = @PlayerId

                SELECT
                    PlayerId,
                    SkillId,
                    Selected
                FROM PlayerSkills
                WHERE 
                    PlayerId = @PlayerId

                SELECT
                    II.PlayerId,
                    II.InventoryItemId,
                    II.ItemId,
                    II.Rating,
                    II.Equipped,
                    II.InventorySlotNumber,
	                COALESCE(ItemModJSON.JSONData, '[]') AS ItemModJSON
                FROM InventoryItems AS II
                OUTER APPLY (
	                SELECT
		                IIM.ItemModId,
		                IIM.ItemSlotId
	                FROM InventoryItemMods AS IIM
	                WHERE II.InventoryItemId = IIM.InventoryItemId
	                FOR JSON PATH
                ) AS ItemModJSON(JSONData)
                WHERE II.PlayerId = @PlayerId";

            var result = Database.QueryToList<Player, PlayerAttribute, PlayerSkill, InventoryItem>(commandText, new QueryParameter("@PlayerId", playerId));

            var sessionData = new SessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                LastUsed = DateTime.UtcNow,
                CurrentZone = 0,
                PlayerData = result.Item1.First(),
                InventoryItems = result.Item4,
                EnemyCooldown = DateTime.UnixEpoch,
                EarliestDefeat = DateTime.UnixEpoch,
                Victory = false,
                Attributes = result.Item2,
                PlayerSkills = result.Item3,
            };

            _cache.SetAndForget($"{SessionPrefix}_{sessionData.SessionId}", sessionData);
            return sessionData;
        }

        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty)
        {
            _cache.SetAndForget($"{SessionPrefix}_{sessionData.SessionId}", sessionData);
            if (inventoryDirty)
            {
                _synchronizer.SynchronizeInventory(sessionData.SessionId);
            }
            if (playerDirty)
            {
                _synchronizer.SynchronizePlayerData(sessionData.SessionId);
            }
            if (skillsDirty)
            {
                _synchronizer.SynchronizeSkills(sessionData.SessionId);
            }
        }

        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash)
        {
            _cache.SetAndForget($"{Constants.CACHE_ACTIVE_ENEMY_PREFIX}_{sessionData.SessionId}", activeEnemyHash);
        }

        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData)
        {
            return _cache.GetDelete($"{Constants.CACHE_ACTIVE_ENEMY_PREFIX}_{sessionData.SessionId}");
        }
    }
}
