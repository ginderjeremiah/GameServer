using GameCore;
using GameCore.DataAccess;
using GameCore.Entities.InventoryItems;
using GameCore.Infrastructure;
using System.Data;

namespace DataAccess.Repositories
{
    internal class InventoryItems : BaseRepository, IInventoryItems
    {
        public static readonly object _inventoryLock = new();
        public static bool _processingInventoryQueue = false;
        public static readonly object _equippedLock = new();
        public static bool _processingEquippedQueue = false;

        public InventoryItems(IDatabaseService database) : base(database) { }

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

            return Database.QueryToList<InventoryItem>(commandText, new QueryParameter("@PlayerId", playerId));
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

            var id = Database.ExecuteScalar<int>(commandText,
                new QueryParameter("@PlayerId", inventoryItem.PlayerId),
                new QueryParameter("@ItemId", inventoryItem.ItemId),
                new QueryParameter("@Rating", inventoryItem.Rating),
                new QueryParameter("@Equipped", inventoryItem.Equipped),
                new QueryParameter("@InventorySlotNumber", inventoryItem.InventorySlotNumber),
                new QueryParameter("@ItemMods", inventoryItem.ItemMods.Serialize(), DbType.String));

            inventoryItem.InventoryItemId = id;
            return id;
        }

        public void UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems)
        {
            var structuredParameter = new QueryParameter("@InventoryItems", StructuredType.InventoryUpdate);

            structuredParameter.AddColumns(
                ("InventoryItemId", DbType.Int32),
                ("InventorySlotNumber", DbType.Int32),
                ("Equipped", DbType.Boolean)
            );

            structuredParameter.AddRows(inventoryItems.Select(item => new List<object?>()
            {
                item.InventoryItemId,
                item.InventorySlotNumber,
                item.Equipped
            }).ToList());

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

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@PlayerId", playerId),
                structuredParameter
            );
        }
    }
}
