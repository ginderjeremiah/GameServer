using DataAccess.Models.InventoryItems;
using DataAccess.Models.Player;
using DataAccess.Models.SessionStore;
using DataAccess.Models.Stats;
using GameLibrary;
using System.Data;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class SessionStore : BaseRepository, ISessionStore
    {
        public SessionStore(string connectionString) : base(connectionString) { }

        public bool TryGetSession(string id, out SessionData session)
        {
            string commandText = @"
                SELECT TOP(1)
                    SS.Id,
                    SS.PlayerId,
                    SS.LastUsed,
                    SS.CurrentZone,
                    SS.ActiveEnemyHash,
                    SS.EnemyCooldown,
                    SS.EarliestDefeat,
                    SS.Victory
                INTO #Session
                FROM SessionStore AS SS
                WHERE SS.Id = @Id
                
                DECLARE @PlayerId INT = (SELECT PlayerId FROM #Session);

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
                WHERE II.PlayerId = @PlayerId

                SELECT * FROM #Session";

            var ds = FillSet(commandText, new SqlParameter("@Id", id));

            if (ds.Tables[0].Rows.Count < 1 || ds.Tables[3].Rows.Count < 1)
            {
                session = null;
                return false;
            }

            session = GetSessionFromResults(ds);

            return true;
        }

        public bool TryGetSession(int playerId, out SessionData session)
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
                WHERE II.PlayerId = @PlayerId

                SELECT TOP(1)
                    SS.Id,
                    SS.PlayerId,
                    SS.LastUsed,
                    SS.CurrentZone,
                    SS.ActiveEnemyHash,
                    SS.EnemyCooldown,
                    SS.EarliestDefeat,
                    SS.Victory
                FROM SessionStore AS SS
                WHERE SS.PlayerId = @PlayerId";

            var ds = FillSet(commandText, new SqlParameter("@PlayerId", playerId));

            if (ds.Tables[0].Rows.Count < 1 || ds.Tables[3].Rows.Count < 1)
            {
                session = null;
                return false;
            }

            session = GetSessionFromResults(ds);

            return true;
        }

        public SessionData GetNewSessionData(string newSessionId, int playerId)
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
            var sessionData = GetSessionFromResults(ds, newSessionId);
            Insert(sessionData);
            return sessionData;
        }

        public void SaveSession(SessionData session)
        {
            session.LastUsed = DateTime.UtcNow;
            var commandText = @"
                UPDATE SessionStore
                SET LastUsed = @LastUsed,
                    CurrentZone = @CurrentZone,
                    ActiveEnemyHash = @ActiveEnemyHash,
                    EnemyCooldown = @EnemyCooldown,
                    EarliestDefeat = @EarliestDefeat,
                    Victory = @Victory
                WHERE Id = @Id";

            ExecuteNonQuery(commandText, GetParameters(session));
        }

        public void SavePlayer(SessionData session)
        {
            session.LastUsed = DateTime.UtcNow;
            var commandText = @"
                UPDATE Players
                SET Level = @Level,
                    Exp = @Exp,
                    StatPointsGained = @StatPointsGained,
                    StatPointsUsed = @StatPointsUsed
                WHERE PlayerId = @PlayerId
            
                UPDATE PlayerBaseStats
                SET Strength = @Strength,
                    Endurance = @Endurance,
                    Intellect = @Intellect,
                    Agility = @Agility,
                    Dexterity = @Dexterity,
                    Luck = @Luck
                WHERE PlayerId = @PlayerId";

            ExecuteNonQuery(commandText, GetParameters(session.PlayerData, session.Stats));
        }

        public void SaveSkills(SessionData session)
        {
            throw new NotImplementedException();
        }

        public void Insert(SessionData session)
        {
            var commandText = @"
                INSERT INTO SessionStore
                VALUES (@Id, @PlayerId, @LastUsed, @CurrentZone, @ActiveEnemyHash, @EnemyCooldown, @EarliestDefeat, @Victory)";

            ExecuteNonQuery(commandText, GetParameters(session));
        }

        private static SqlParameter[] GetParameters(SessionData session)
        {
            return new SqlParameter[]
            {
                new SqlParameter("@Id", session.SessionId),
                new SqlParameter("@PlayerId", session.PlayerData.PlayerId),
                new SqlParameter("@LastUsed", session.LastUsed),
                new SqlParameter("@CurrentZone", session.CurrentZone),
                new SqlParameter("@ActiveEnemyHash", session.ActiveEnemyHash),
                new SqlParameter("@EnemyCooldown", session.EnemyCooldown),
                new SqlParameter("@EarliestDefeat", session.EarliestDefeat),
                new SqlParameter("@Victory", session.Victory)
            };
        }

        private static SqlParameter[] GetParameters(Player playerData, BaseStats stats)
        {
            return new SqlParameter[]
            {
                new SqlParameter("@PlayerId", playerData.PlayerId),
                new SqlParameter("@Level", playerData.Level),
                new SqlParameter("@Exp", playerData.Exp),
                new SqlParameter("@StatPointsGained", playerData.StatPointsGained),
                new SqlParameter("@StatPointsUsed", playerData.StatPointsUsed),
                new SqlParameter("@Strength", stats.Strength),
                new SqlParameter("@Endurance", stats.Endurance),
                new SqlParameter("@Intellect", stats.Intellect),
                new SqlParameter("@Agility", stats.Agility),
                new SqlParameter("@Dexterity", stats.Dexterity),
                new SqlParameter("@Luck", stats.Luck)
            };
        }

        private static SessionData GetSessionFromResults(DataSet ds, string? newSessionId = null)
        {
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

            if (newSessionId is null)
            {
                var sessionRow = ds.Tables[3].Rows[0];
                var sessionId = sessionRow["Id"].AsString();
                var currentZone = sessionRow["CurrentZone"].AsInt();
                var activeEnemyHash = sessionRow["ActiveEnemyHash"].AsString();
                var enemyCooldown = sessionRow["EnemyCooldown"].AsDate();
                var earliestDefeat = sessionRow["EarliestDefeat"].AsDate();
                var victory = sessionRow["Victory"].AsBool();
                return new SessionData(sessionId, playerData, inventoryItems, stats, selectedSkills, currentZone, activeEnemyHash, enemyCooldown, earliestDefeat, victory);
            }
            else
            {
                return new SessionData(newSessionId, playerData, inventoryItems, stats, selectedSkills);
            }
        }
    }

    public interface ISessionStore
    {
        public bool TryGetSession(string id, out SessionData session);
        public bool TryGetSession(int playerId, out SessionData session);
        public SessionData GetNewSessionData(string newSessionId, int playerId);
        public void SaveSession(SessionData session);
        public void SavePlayer(SessionData session);
        public void SaveSkills(SessionData session);
        public void Insert(SessionData session);
    }
}
