using DataAccess.Models.InventoryItems;
using Microsoft.SqlServer.Server;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;

namespace DataAccess.Repositories
{
    internal class InventoryItems : BaseRepository, IInventoryItems
    {
        public InventoryItems(string connectionString) : base(connectionString) { }

        public List<InventoryItem> GetInventory(int playerId)
        {
            var commandText = @"
                SELECT
                    InventoryItemId,
                    PlayerId,
                    ItemId,
                    Rating,
                    Equipped,
                    SlotId
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
			            itemSlotId,
			            itemModId
		            FROM OPENJSON(@ItemMods)
		            WITH ( itemModId INT, itemSlotId INT)
	            END

                INSERT INTO InventoryItems
                VALUES
                    (@PlayerId, @ItemId, @Rating, @Equipped, @SlotId)
                
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
                new SqlParameter("@SlotId", inventoryItem.SlotId),
                new SqlParameter("@ItemMods", JsonSerializer.Serialize(inventoryItem.ItemMods)));

            inventoryItem.InventoryItemId = id;
            return id;
        }
        public void UpdateEquippedItemSlots(int playerId, IEnumerable<InventoryItem> equippedItems)
        {
            var data = new SqlMetaData[2];
            data[0] = new SqlMetaData("InventoryItemId", SqlDbType.Int);
            data[1] = new SqlMetaData("SlotId", SqlDbType.Int);

            var records = equippedItems.Select(item =>
            {
                var record = new SqlDataRecord(data);
                record.SetInt32(0, item.InventoryItemId);
                record.SetInt32(1, item.SlotId);
                return record;
            }).ToArray();

            var commandText = @"
                UPDATE InventoryItems
                SET Equipped = 0
                WHERE PlayerId = @PlayerId

                UPDATE II
                SET Equipped = 1,
                    SlotId = EI.SlotId
                FROM InventoryItems II
                INNER JOIN @EquippedItems EI
                ON II.InventoryItemId = EI.InventoryItemId";

            ExecuteNonQuery(commandText, new SqlParameter("@PlayerId", playerId), new SqlParameter("@EquippedItems", SqlDbType.Structured) { Value = records, TypeName = "InventoryUpdate" });
        }
        public void UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems)
        {
            var data = new SqlMetaData[2];
            data[0] = new SqlMetaData("InventoryItemId", SqlDbType.Int);
            data[1] = new SqlMetaData("SlotId", SqlDbType.Int);

            var records = inventoryItems.Select(item =>
            {
                var record = new SqlDataRecord(data);
                record.SetInt32(0, item.InventoryItemId);
                record.SetInt32(1, item.SlotId);
                return record;
            }).ToArray();

            var commandText = @"
                UPDATE II
                SET SlotId = INVI.SlotId
                FROM InventoryItems II
                INNER JOIN @InventoryItems INVI
                ON II.InventoryItemId = INVI.InventoryItemId";

            ExecuteNonQuery(commandText, new SqlParameter("@PlayerId", playerId), new SqlParameter("@InventoryItems", SqlDbType.Structured) { Value = records, TypeName = "InventoryUpdate" });
        }
    }

    public interface IInventoryItems
    {
        public List<InventoryItem> GetInventory(int playerId);
        public int AddInventoryItem(InventoryItem inventoryItem);
        public void UpdateEquippedItemSlots(int playerId, IEnumerable<InventoryItem> equippedItems);
        public void UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems);
    }
}
