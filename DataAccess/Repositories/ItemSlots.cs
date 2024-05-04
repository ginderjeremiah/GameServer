using DataAccess.Entities.ItemSlots;
using GameCore.Database;
using GameCore.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class ItemSlots : BaseRepository, IItemSlots
    {
        private static readonly List<List<ItemSlot>?> _itemSlots = new();
        private static readonly object _lockForItemSlot = new();

        public ItemSlots(IDataProvider database) : base(database) { }

        public void AddItemSlot(int itemId, int slotTypeId, int guaranteedId, decimal probability)
        {
            var commandText = @"
                INSERT INTO ItemSlots
                VALUES
                    (@ItemId, @SlotTypeId, @GuaranteedId, @Probability)";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemId", itemId),
                new QueryParameter("@SlotTypeId", slotTypeId),
                new QueryParameter("@GuaranteedId", guaranteedId),
                new QueryParameter("@Probability", probability)
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

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemId", itemId),
                new QueryParameter("@SlotTypeId", slotTypeId),
                new QueryParameter("@GuaranteedId", guaranteedId),
                new QueryParameter("@Probability", probability),
                new QueryParameter("@ItemSlotId", itemSlotId)
            );
        }

        public void DeleteItemSlot(int itemSlotId)
        {
            var commandText = @"
                DELETE ItemSlots
                WHERE ItemSlotId = @ItemSlotId";

            Database.ExecuteNonQuery(commandText, new QueryParameter("@ItemSlotId", itemSlotId));
        }

        public List<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            if (itemId >= _itemSlots.Count || _itemSlots[itemId] is null || refreshCache)
            {
                lock (_lockForItemSlot)
                {
                    for (int i = _itemSlots.Count; i <= itemId + 1; i++)
                    {
                        _itemSlots.Add(null);
                    }

                    if (_itemSlots[itemId] is null || refreshCache)
                        _itemSlots[itemId] = GetSlotsForItem(itemId);
                }
            }
            return _itemSlots[itemId];
        }

        private List<ItemSlot> GetSlotsForItem(int itemId)
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

            return Database.QueryToList<ItemSlot>(commandText, new QueryParameter("@ItemId", itemId));
        }
    }

    public interface IItemSlots
    {
        public void AddItemSlot(int itemId, int slotTypeId, int guaranteedId, decimal probability);
        public void UpdateItemSlot(int itemSlotId, int itemId, int slotTypeId, int guaranteedId, decimal probability);
        public void DeleteItemSlot(int itemSlotId);
        public List<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false);
    }
}
