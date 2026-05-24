using Game.Core.Players.Inventories;

namespace Game.Api.Models.InventoryItems
{
    public class InventoryUpdate : IModel, IInventoryUpdate
    {
        public int Id { get; set; }
        public int InventorySlotNumber { get; set; }
        public bool Equipped { get; set; }

        // Explicit implementation maps InventorySlotNumber → IInventoryUpdate.SlotNumber
        int IInventoryUpdate.SlotNumber => InventorySlotNumber;
    }
}
