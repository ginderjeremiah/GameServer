using Game.Core.Items;

namespace Game.Core.Events
{
    /// <summary>
    /// Raised when an item is added to a player's inventory.
    /// </summary>
    /// <param name="PlayerId">The player who received the item.</param>
    /// <param name="Item">The item that was acquired.</param>
    /// <param name="InventoryItemId">The database identifier of the persisted inventory record.</param>
    /// <param name="SlotNumber">The storage slot the item was placed in.</param>
    public record ItemAcquiredEvent(
        int PlayerId,
        Item Item,
        int InventoryItemId,
        int SlotNumber) : IDomainEvent;
}
