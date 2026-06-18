using Game.Core.Items;

namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents a modifier that has been applied to a specific slot on an item.
    /// </summary>
    public class AppliedModSlot
    {
        public required int ItemModId { get; init; }
        public required int ItemModSlotId { get; init; }
        public required ItemMod ItemMod { get; init; }
    }
}
