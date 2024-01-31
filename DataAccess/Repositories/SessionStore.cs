using DataAccess.Models.InventoryItems;
using DataAccess.Models.Players;
using DataAccess.Models.SessionStore;
using DataAccess.Models.Stats;
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
                    P.PlayerId,
                    P.PlayerName,
                    P.UserName,
                    P.Level,
                    P.Salt,
                    P.PassHash,
	                P.Level,
	                P.Exp,
	                P.StatPointsGained,
	                P.StatPointsUsed,
	                PBS.Strength,
	                PBS.Endurance,
	                PBS.Intellect,
	                PBS.Agility,
	                PBS.Dexterity,
	                PBS.Luck
                FROM Players AS P
                INNER JOIN PlayerBaseStats AS PBS
	                ON P.PlayerId = PBS.PlayerId
                WHERE P.PlayerId = @PlayerId

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


            var stats = playerRow.To<BaseStats>();
            var selectedSkills = ds.Tables[1].AsEnumerable().Select(row => row["SkillId"].AsInt()).ToList();

            var inventoryItems = ds.Tables[2].AsEnumerable()
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
                Stats = stats,
                SelectedSkills = selectedSkills,
            };

            _redisStore.SetAndForget($"{SessionPrefix}_{sessionData.SessionId}", sessionData);
            return sessionData;
        }

        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty, bool equippedDirty)
        {
            _redisStore.SetAndForget($"{SessionPrefix}_{sessionData.SessionId}", sessionData);
            if (playerDirty)
            {
                _redisStore.AddToQueue(Constants.REDIS_PLAYER_QUEUE, sessionData.SessionId);
                _redisStore.Publish(Constants.REDIS_PLAYER_CHANNEL, "");
            }
            if (skillsDirty)
            {
                _redisStore.AddToQueue(Constants.REDIS_SKILLS_QUEUE, sessionData.SessionId);
                _redisStore.Publish(Constants.REDIS_SKILLS_CHANNEL, "");
            }
            if (inventoryDirty)
            {
                _redisStore.AddToQueue(Constants.REDIS_INVENTORY_QUEUE, sessionData.SessionId);
                _redisStore.Publish(Constants.REDIS_INVENTORY_CHANNEL, "");
            }
            if (equippedDirty)
            {
                _redisStore.AddToQueue(Constants.REDIS_EQUIPPED_QUEUE, sessionData.SessionId);
                _redisStore.Publish(Constants.REDIS_EQUIPPED_CHANNEL, "");
            }
        }
    }

    public interface ISessionStore
    {
        public bool TryGetSession(string id, out SessionData session);
        public SessionData GetNewSessionData(int playerId);
        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty, bool equippedDirty);
    }
}
