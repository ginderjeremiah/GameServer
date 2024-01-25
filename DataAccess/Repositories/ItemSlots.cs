using GameServer.Models;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class ItemSlots : BaseRepository, IItemSlots
    {
        public ItemSlots(string connectionString) : base(connectionString) { }

        public List<ItemSlot> SlotsForItem(int itemId)
        {
            var commandText = @"
                SELECT
                    ItemSlotId,
                    ItemId,
                    SlotTypeId,
                    GuaranteedId,
                    Probability
                FROM
                    ItemSlots
                WHERE
                    ItemId = @ItemId";

            return QueryToList<ItemSlot>(commandText, new SqlParameter("@ItemId", itemId));
        }

        public void AddItemSlot(int itemId, int slotTypeId, int guaranteedId, decimal probability)
        {
            var commandText = @"
                INSERT INTO ItemSlots
                VALUES
                    (@ItemId, @SlotTypeId, @GuaranteedId, @Probability)";

            ExecuteNonQuery(commandText,
                            new SqlParameter("@ItemId", itemId),
                            new SqlParameter("@SlotTypeId", slotTypeId),
                            new SqlParameter("@GuaranteedId", guaranteedId),
                            new SqlParameter("@Probability", probability)
                        );
        }

        public void UpdateItemSlot(int itemSlotId, int itemId, int slotTypeId, int guaranteedId, decimal probability)
        {
            var commandText = @"
                UPDATE ItemSlots
                SET ItemId = @ItemId,
                    SlotTypeId = @SlotTypeId,
                    GuaranteedId = @GuaranteedId,
                    Probability = @Probability
                WHERE ItemSlotId = @ItemSlotId";

            ExecuteNonQuery(commandText,
                            new SqlParameter("@ItemId", itemId),
                            new SqlParameter("@SlotTypeId", slotTypeId),
                            new SqlParameter("@GuaranteedId", guaranteedId),
                            new SqlParameter("@Probability", probability),
                            new SqlParameter("@ItemSlotId", itemSlotId)
                        );
        }

        public void DeleteItemSlot(int itemSlotId)
        {
            var commandText = @"
                DELETE ItemSlots
                WHERE ItemSlotId = @ItemSlotId";

            ExecuteNonQuery(commandText, new SqlParameter("@ItemSlotId", itemSlotId));
        }
    }

    public interface IItemSlots
    {
        public List<ItemSlot> SlotsForItem(int itemId);
        public void AddItemSlot(int itemId, int slotTypeId, int guaranteedId, decimal probability);
        public void UpdateItemSlot(int itemSlotId, int itemId, int slotTypeId, int guaranteedId, decimal probability);
        public void DeleteItemSlot(int itemSlotId);
    }
}
