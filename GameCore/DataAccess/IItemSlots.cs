using GameCore.Entities.ItemSlots;

namespace GameCore.DataAccess
{
    public interface IItemSlots
    {
        public void AddItemSlot(int itemId, int slotTypeId, int guaranteedId, decimal probability);
        public void UpdateItemSlot(int itemSlotId, int itemId, int slotTypeId, int guaranteedId, decimal probability);
        public void DeleteItemSlot(int itemSlotId);
        public List<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false);
    }
}
