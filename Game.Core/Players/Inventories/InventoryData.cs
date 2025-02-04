using Game.Core.Items;
using Game.Core.Players.Inventories;

namespace Game.Core.Inventories
{
    /// <summary>
    /// Represents the data for an item in a <see cref="Inventory"/>.
    /// </summary>
    public class InventoryData
    {
        /// <summary>
        /// The item this data corresponds to.
        /// </summary>
        public required Item Item { get; set; }

        /// <summary>
        /// The number of times this item has been found.
        /// </summary>
        public required int TimesFound { get; set; }

        /// <summary>
        /// Whether this item is unlocked and should be visible.
        /// </summary>
        public required bool Unlocked { get; set; }
    }
}
