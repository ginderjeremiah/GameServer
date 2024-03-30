using DataAccess.Models.InventoryItems;
using DataAccess.Models.PlayerAttributes;
using DataAccess.Models.Players;
using DataAccess.Models.SessionStore;
using DataAccess.Redis;
using GameLibrary;
using System.Data;
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
                    SkillId
                FROM PlayerSkills
                WHERE 
                    PlayerId = @PlayerId
                    AND Selected = 1

                SELECT
                    II.InventoryItemId,
                    II.ItemId,
                    II.Rating,
                    II.Equipped,
                    II.SlotId,
                    IIM.ItemModId,
                    IIM.ItemSlotId
                FROM InventoryItems AS II
                LEFT JOIN InventoryItemMods AS IIM
                ON II.InventoryItemId = IIM.InventoryItemId
                WHERE II.PlayerId = @PlayerId";

            var ds = FillSet(commandText, new SqlParameter("@PlayerId", playerId));
            var playerRow = ds.Tables[0].Rows[0];
            var playerData = new Player
            {
                PlayerId = playerRow["PlayerId"].AsInt(),
                UserName = playerRow["UserName"].AsString(),
                Salt = new Guid(playerRow["Salt"].AsString()),
                PassHash = playerRow["PassHash"].AsString(),
                PlayerName = playerRow["PlayerName"].AsString(),
                Level = playerRow["Level"].AsInt(),
                Exp = playerRow["Exp"].AsInt(),
                StatPointsGained = playerRow["StatPointsGained"].AsInt(),
                StatPointsUsed = playerRow["StatPointsUsed"].AsInt(),
            };


            var attributes = ds.Tables[1].AsEnumerable().Select(row => new PlayerAttribute
            {
                PlayerId = row["PlayerId"].AsInt(),
                AttributeId = row["AttributeId"].AsInt(),
                Amount = row["Amount"].AsDecimal()
            }).ToList();

            var selectedSkills = ds.Tables[2].AsEnumerable().Select(row => row["SkillId"].AsInt()).ToList();

            var inventoryItems = ds.Tables[3].AsEnumerable()
                    .GroupBy(row => row["InventoryItemId"].AsInt())
                    .Select(g => new InventoryItem
                    {
                        InventoryItemId = g.Key,
                        ItemId = g.First()["ItemId"].AsInt(),
                        Rating = g.First()["Rating"].AsInt(),
                        Equipped = g.First()["Equipped"].AsBool(),
                        SlotId = g.First()["SlotId"].AsInt(),
                        ItemMods = g.Where(r => r["ItemModId"] is not null and not DBNull)
                            .Select(r => new InventoryItemMod()
                            {
                                ItemModId = r["ItemModId"].AsInt(),
                                ItemSlotId = r["ItemSlotId"].AsInt()
                            }).ToList()
                    }).ToList();

            var sessionData = new SessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                LastUsed = DateTime.UtcNow,
                CurrentZone = 0,
                PlayerData = playerData,
                InventoryItems = inventoryItems,
                EnemyCooldown = DateTime.UnixEpoch,
                ActiveEnemyHash = "",
                EarliestDefeat = DateTime.UnixEpoch,
                Victory = false,
                Attributes = attributes,
                SelectedSkills = selectedSkills,
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
    }

    public interface ISessionStore
    {
        public bool TryGetSession(string id, out SessionData session);
        public SessionData GetNewSessionData(int playerId);
        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty);
    }
}
