using DataAccess.Entities.InventoryItems;
using DataAccess.Redis;
using GameLibrary;
using Microsoft.SqlServer.Server;
using StackExchange.Redis;
using System.Data;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class InventoryItems : BaseRepository, IInventoryItems
    {
        public static readonly object _inventoryLock = new();
        public static bool _processingInventoryQueue = false;
        public static readonly object _equippedLock = new();
        public static bool _processingEquippedQueue = false;
        private readonly RepositoryManager _repositoryManager;

        public InventoryItems(string connectionString, RepositoryManager repos) : base(connectionString)
        {
            _repositoryManager = repos;
        }

        [RedisSubscriber(Constants.REDIS_INVENTORY_CHANNEL, Constants.REDIS_INVENTORY_QUEUE)]
        internal static void ProcessInventoryUpdate(RepositoryManager repos, RedisValue queueValue)
        {
            if (repos.SessionStore.TryGetSession(queueValue, out var sessionData))
                repos.InventoryItems.UpdateInventoryItemSlots(sessionData.PlayerData.PlayerId, sessionData.InventoryItems);
        }

        public List<InventoryItem> GetInventory(int playerId)
        {
            var commandText = @"
                SELECT
                    InventoryItemId,
                    PlayerId,
                    ItemId,
                    Rating,
                    Equipped,
                    InventorySlotNumber
                FROM InventoryItems
                WHERE
                    PlayerId = @PlayerId";

            return QueryToList<InventoryItem>(commandText, new SqlParameter("@PlayerId", playerId));
        }

        public int AddInventoryItem(InventoryItem inventoryItem)
        {
            var commandText = @"
                CREATE TABLE #ItemModsJson (
		            ItemModId INT,
		            ItemSlotId INT
	            )

	            IF (@ItemMods <> '')
	            BEGIN
		            INSERT INTO #ItemModsJson
		            (ItemModId, ItemSlotId)
		            SELECT
			            ItemModId,
			            ItemSlotId
		            FROM OPENJSON(@ItemMods)
		            WITH ( itemModId INT, itemSlotId INT)
	            END

                INSERT INTO InventoryItems
                VALUES
                    (@PlayerId, @ItemId, @Rating, @Equipped, @InventorySlotNumber)
                
                DECLARE @Id INT = SCOPE_IDENTITY()
 
                INSERT INTO InventoryItemMods
                (InventoryItemId, ItemSlotId, ItemModId)
                SELECT
                    @Id,
                    itemSlotId,
                    itemModId
                FROM
                    #ItemModsJson

                SELECT @Id;";

            var id = ExecuteScalar<int>(commandText,
                new SqlParameter("@PlayerId", inventoryItem.PlayerId),
                new SqlParameter("@ItemId", inventoryItem.ItemId),
                new SqlParameter("@Rating", inventoryItem.Rating),
                new SqlParameter("@Equipped", inventoryItem.Equipped),
                new SqlParameter("@InventorySlotNumber", inventoryItem.InventorySlotNumber),
                new SqlParameter("@ItemMods", inventoryItem.ItemMods.Serialize()));

            inventoryItem.InventoryItemId = id;
            return id;
        }

        public void UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems)
        {
            var data = new SqlMetaData[3];
            data[0] = new SqlMetaData("InventoryItemId", SqlDbType.Int);
            data[1] = new SqlMetaData("InventorySlotNumber", SqlDbType.Int);
            data[2] = new SqlMetaData("Equipped", SqlDbType.Bit);

            var records = inventoryItems.Select(item =>
            {
                var record = new SqlDataRecord(data);
                record.SetInt32(0, item.InventoryItemId);
                record.SetInt32(1, item.InventorySlotNumber);
                record.SetBoolean(2, item.Equipped);
                return record;
            }).ToArray();

            if (records.Length == 0)
            {
                records = null;
            }

            var commandText = @"
                DELETE II
                FROM InventoryItems II
                LEFT JOIN @InventoryItems INVI
                ON II.InventoryItemId = INVI.InventoryItemId
                WHERE II.PlayerId = @PlayerId
                AND INVI.InventoryItemId IS NULL

                UPDATE II
                SET InventorySlotNumber = INVI.InventorySlotNumber,
                    Equipped = INVI.Equipped
                FROM InventoryItems II
                INNER JOIN @InventoryItems INVI
                ON II.InventoryItemId = INVI.InventoryItemId";

            ExecuteNonQuery(commandText,
                new SqlParameter("@PlayerId", playerId),
                new SqlParameter("@InventoryItems", SqlDbType.Structured) { Value = records, TypeName = "InventoryUpdate" }
            );
        }

        public List<InventoryItem> RollDrops(int enemyId, int zoneId, int max)
        {
            var rng = new Random();
            var drops = new List<InventoryItem>();
            foreach (var drop in _repositoryManager.Enemies.GetEnemy(enemyId).EnemyDrops.Where(d => (decimal)rng.NextSingle() < d.DropRate))
            {
                if (drops.Count < max)
                {
                    drops.Add(GetItemInstance(drop.ItemId, rng));
                }
            }
            foreach (var drop in _repositoryManager.Zones.GetZone(zoneId).ZoneDrops.Where(d => (decimal)rng.NextSingle() < d.DropRate))
            {
                if (drops.Count < max)
                {
                    drops.Add(GetItemInstance(drop.ItemId, rng));
                }
            }
            return drops;
        }

        private InventoryItem GetItemInstance(int itemId, Random rng)
        {
            var slots = _repositoryManager.ItemSlots.SlotsForItem(itemId);
            var itemMods = new List<int>();
            var inventoryItemMods = new List<InventoryItemMod>();

            foreach (var slot in slots.Where(s => (decimal)rng.NextSingle() < s.Probability))
            {
                int? modId = null;
                if (slot.GuaranteedId == -1)
                {
                    var mods = _repositoryManager.ItemMods.GetModsForItemBySlot(slot.ItemId);
                    if (mods.TryGetValue(slot.SlotTypeId, out var modsForSlot))
                    {
                        //TODO Add weights for item mods
                        var actualMods = modsForSlot.Where(mod => !itemMods.Contains(mod.ItemModId)).ToList();
                        if (actualMods.Any())
                        {
                            modId = actualMods[rng.Next(0, actualMods.Count - 1)].ItemModId;
                        }
                    }
                }
                else
                {
                    modId = _repositoryManager.ItemMods.AllItemMods()[slot.GuaranteedId].ItemModId;
                }

                if (modId is not null)
                {
                    itemMods.Add(modId.Value);
                    inventoryItemMods.Add(new InventoryItemMod
                    {
                        ItemModId = modId.Value,
                        ItemSlotId = slot.ItemSlotId,
                    });
                }
            }

            return new InventoryItem
            {
                ItemId = itemId,
                Rating = 0, //TODO: implement Rating calculation
                Equipped = false,
                ItemMods = inventoryItemMods
            };
        }
    }

    public interface IInventoryItems
    {
        public List<InventoryItem> GetInventory(int playerId);
        public int AddInventoryItem(InventoryItem inventoryItem);
        public void UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems);
        public List<InventoryItem> RollDrops(int enemyId, int zoneId, int max);
    }
}
