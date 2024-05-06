using GameCore.Sessions;

namespace GameServer.Models.InventoryItems
{
    public class InventoryUpdate : IModel, IInventoryUpdate
    {
        public int InventoryItemId { get; set; }
        public int InventorySlotNumber { get; set; }
        public bool Equipped { get; set; }
    }
}
