using Game.Core.Items;

namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents an item that a player has unlocked, along with any mods currently applied to it.
    /// </summary>
    public class UnlockedItemSlot
    {
        /// <summary>The unlocked item. The single source of truth; <see cref="ItemId"/> derives from it.</summary>
        public required Item Item { get; set; }

        /// <summary>The id of the unlocked <see cref="Item"/>, derived rather than stored independently.</summary>
        public int ItemId => Item.Id;

        public required List<AppliedModSlot> AppliedMods { get; set; }

        /// <summary>Whether the player has favorited this item.</summary>
        public bool Favorite { get; set; }
    }
}
