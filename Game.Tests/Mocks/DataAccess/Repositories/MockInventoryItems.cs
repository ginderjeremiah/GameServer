using GameCore.DataAccess;
using GameCore.Entities;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockInventoryItems : IInventoryItems
    {
        public MockInventoryItems() { }
        public List<InventoryItem> GetInventory(int playerId)
        {
            throw new NotImplementedException();
        }

        public int AddInventoryItem(InventoryItem item)
        {
            throw new NotImplementedException();
        }

        public void UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems)
        {
            throw new NotImplementedException();
        }
    }
}
