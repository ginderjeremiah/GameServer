namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents a requested change to an inventory item's slot assignment.
    /// </summary>
    public interface IInventoryUpdate
    {
        /// <summary>The InventoryItem record ID being moved.</summary>
        int Id { get; }

        /// <summary>
        /// The destination slot number.
        /// For equipped items this is the <see cref="EEquipmentSlot"/> integer value.
        /// For storage items this is an arbitrary non-negative index.
        /// </summary>
        int SlotNumber { get; }

        /// <summary>Whether the item should be placed in an equipment slot.</summary>
        bool Equipped { get; }
    }
}
