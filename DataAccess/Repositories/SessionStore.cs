using DataAccess.Entities.InventoryItems;
using DataAccess.Entities.PlayerAttributes;
using DataAccess.Entities.Players;
using DataAccess.Entities.SessionStore;
using DataAccess.Redis;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class SessionStore : BaseRepository, ISessionStore
    {
        private readonly RedisStore _redisStore;
        private static string SessionPrefix => Constants.REDIS_SESSION_PREFIX;
        public SessionStore(string connectionString, RedisStore redisStore) : base(connectionString)
        {
            _redisStore = redisStore;
        }

        public bool TryGetSession(string id, out SessionData session)
        {
            return _redisStore.TryGet($"{SessionPrefix}_{id}", out session);
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
                    II.SlotId,
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

            var result = QueryToList<Player, PlayerAttribute, PlayerSkill, InventoryItem>(commandText, new SqlParameter("@PlayerId", playerId));

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

            _redisStore.SetAndForget($"{SessionPrefix}_{sessionData.SessionId}", sessionData);
            return sessionData;
        }

        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty)
        {
            _redisStore.SetAndForget($"{SessionPrefix}_{sessionData.SessionId}", sessionData);
            if (playerDirty)
            {
                if (_redisStore.TryGetQueue(Constants.REDIS_PLAYER_QUEUE, out var queue))
                    queue.AddToQueue(sessionData.SessionId);
            }
            if (skillsDirty)
            {
                if (_redisStore.TryGetQueue(Constants.REDIS_SKILLS_QUEUE, out var queue))
                    queue.AddToQueue(sessionData.SessionId);
            }
            if (inventoryDirty)
            {
                if (_redisStore.TryGetQueue(Constants.REDIS_INVENTORY_QUEUE, out var queue))
                    queue.AddToQueue(sessionData.SessionId);
            }
        }

        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash)
        {
            _redisStore.SetAndForget($"{Constants.REDIS_ACTIVE_ENEMY_PREFIX}_{sessionData.SessionId}", activeEnemyHash);
        }

        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData)
        {
            return _redisStore.GetDelete<string>($"{Constants.REDIS_ACTIVE_ENEMY_PREFIX}_{sessionData.SessionId}");
        }
    }

    public interface ISessionStore
    {
        public bool TryGetSession(string id, out SessionData session);
        public SessionData GetNewSessionData(int playerId);
        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty);
        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash);
        public string GetAndDeleteActiveEnemyHash(SessionData sessionData);
    }
}
