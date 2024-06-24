using GameCore.DataAccess;
using GameCore.Entities;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemSlots : IItemSlots
    {
        public List<ItemSlot> ItemSlots { get; set; } = new();
        public bool Refreshed { get; set; } = false;
        public void AddItemSlot(int itemId, int slotTypeId, int guaranteedId, decimal probability)
        {
            var maxId = ItemSlots.Max(slot => slot.ItemSlotId) + 1;
            ItemSlots.Add(new ItemSlot
            {
                ItemSlotId = maxId,
                ItemId = itemId,
                SlotTypeId = slotTypeId,
                GuaranteedId = guaranteedId,
                Probability = probability
            });
        }

        public void DeleteItemSlot(int itemSlotId)
        {
            var slotToRemove = ItemSlots.FirstOrDefault(item => item.ItemSlotId == itemSlotId);
            if (slotToRemove != null)
            {
                ItemSlots.Remove(slotToRemove);
            }
        }

        public List<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            Refreshed = refreshCache;
            return ItemSlots.Where(slot => slot.ItemId == itemId).ToList();
        }

        public void UpdateItemSlot(int itemSlotId, int itemId, int slotTypeId, int guaranteedId, decimal probability)
        {
            var slotToUpdate = ItemSlots.FirstOrDefault(item => item.ItemSlotId == itemSlotId);
            if (slotToUpdate != null)
            {
                slotToUpdate.ItemId = itemId;
                slotToUpdate.SlotTypeId = slotTypeId;
                slotToUpdate.GuaranteedId = guaranteedId;
                slotToUpdate.Probability = probability;
            }
        }
    }
}
