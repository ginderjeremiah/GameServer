using GameCore.Entities.InventoryItems;

namespace GameCore.DataAccess
{
    public interface IInventoryItems
    {
        public List<InventoryItem> GetInventory(int playerId);
        public int AddInventoryItem(InventoryItem inventoryItem);
        public void UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems);
    }
}
