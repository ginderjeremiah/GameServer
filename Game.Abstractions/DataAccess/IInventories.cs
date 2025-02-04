using Game.Core.Items;
using Game.Core.Players.Inventories;

namespace Game.Abstractions.DataAccess
{
    public interface IInventories
    {
        public Task<Inventory> GetPlayerInventory(int playerId);
        public Task<int> AddInventoryItem(Item inventoryItem);
        public Task UpdateInventoryItemSlots(int playerId, IEnumerable<Item> inventoryItems);
    }
}
