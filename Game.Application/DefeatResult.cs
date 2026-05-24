using Game.Core.Items;

namespace Game.Application
{
    /// <summary>
    /// Carries the outcome of a successful enemy defeat: the exp reward and
    /// every item that was added to the player's inventory as a drop.
    /// </summary>
    public class DefeatResult
    {
        public required int ExpReward { get; set; }
        public required IReadOnlyList<DroppedItemInfo> DroppedItems { get; set; }
    }

    /// <summary>
    /// Represents a single item drop that has been persisted to the inventory.
    /// </summary>
    public class DroppedItemInfo
    {
        /// <summary>The newly created <c>InventoryItem</c> database record ID.</summary>
        public required int InventoryItemId { get; set; }

        /// <summary>The storage slot number assigned in the player's inventory.</summary>
        public required int SlotNumber { get; set; }

        /// <summary>The domain item that was dropped.</summary>
        public required Item Item { get; set; }
    }
}
