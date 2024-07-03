using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IInventoryItems
    {
        public Task<IEnumerable<InventoryItem>> GetInventoryAsync(int playerId);
        public Task<int> AddInventoryItemAsync(InventoryItem inventoryItem);
        public Task UpdateInventoryItemSlotsAsync(int playerId, IEnumerable<InventoryItem> inventoryItems);
    }
}
