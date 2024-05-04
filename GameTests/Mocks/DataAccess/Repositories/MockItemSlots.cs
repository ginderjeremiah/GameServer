using DataAccess.Entities.ItemSlots;
using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemSlots : IItemSlots
    {
        public void AddItemSlot(int itemId, int slotTypeId, int guaranteedId, decimal probability)
        {
            throw new NotImplementedException();
        }

        public void DeleteItemSlot(int itemSlotId)
        {
            throw new NotImplementedException();
        }

        public List<ItemSlot> SlotsForItem(int itemId, bool refreshCache = false)
        {
            throw new NotImplementedException();
        }

        public void UpdateItemSlot(int itemSlotId, int itemId, int slotTypeId, int guaranteedId, decimal probability)
        {
            throw new NotImplementedException();
        }
    }
}
