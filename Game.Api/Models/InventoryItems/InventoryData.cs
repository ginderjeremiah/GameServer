using Game.Api.Models;
using Game.Core.Sessions;

namespace Game.Api.Models.InventoryItems
{
    public class InventoryData : IModel
    {
        public List<InventoryItem?> Inventory { get; set; }
        public List<InventoryItem?> Equipped { get; set; }

        public InventoryData() { }

        public InventoryData(SessionInventory sessionInventory)
        {
            Inventory = sessionInventory.Inventory.Select(item => item is null ? null : new InventoryItem(item)).ToList();
            Equipped = sessionInventory.Equipped.Select(item => item is null ? null : new InventoryItem(item)).ToList();
        }
    }
}
