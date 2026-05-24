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
    }
}
