using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IInventoryItems
    {
        public Task<IEnumerable<InventoryItem>> GetPlayerInventory(int playerId);
        public Task<int> AddInventoryItem(InventoryItem inventoryItem);
        public Task UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems);
    }
}
