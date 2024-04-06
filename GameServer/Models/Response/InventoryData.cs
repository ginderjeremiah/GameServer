using DataAccess.Models.InventoryItems;
using GameServer.Auth;

namespace GameServer.Models.Response
{
    public class InventoryData
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
