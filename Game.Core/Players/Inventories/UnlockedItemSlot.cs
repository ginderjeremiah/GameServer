using Game.Core.Items;

namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents an item that a player has unlocked, along with any mods currently applied to it.
    /// </summary>
    public class UnlockedItemSlot
    {
        public required int ItemId { get; set; }
        public required Item Item { get; set; }
        public required List<AppliedModSlot> AppliedMods { get; set; }

        /// <summary>Whether the player has favorited this item.</summary>
        public bool Favorite { get; set; }
    }
}
