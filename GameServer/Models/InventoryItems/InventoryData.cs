using GameServer.Auth;

namespace GameServer.Models.InventoryItems
{
    public class InventoryData : IModel
    {
        public List<InventoryItem?> Inventory { get; set; }
        public List<InventoryItem?> Equipped { get; set; }

        public InventoryData(SessionInventory sessionInventory)
        {
            Inventory = sessionInventory.Inventory;
            Equipped = sessionInventory.Equipped;
        }
    }
}
