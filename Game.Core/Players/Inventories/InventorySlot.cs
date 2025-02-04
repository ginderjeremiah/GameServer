using Game.Core.Items;

namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents a slot in an inventory.
    /// </summary>
    public class InventorySlot
    {
        /// <summary>
        /// The slot number of the inventory slot.
        /// </summary>
        public int SlotNumber { get; set; }

        /// <summary>
        /// The item stored in this inventory slot.
        /// </summary>
        public Item? Item { get; set; }
    }
}
