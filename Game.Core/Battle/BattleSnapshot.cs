using Game.Core.Players;

namespace Game.Core.Battle
{
    /// <summary>
    /// A minimal snapshot of the player's battle-relevant state captured at battle start.
    /// Stores IDs and raw allocations so the full <see cref="Battler"/> can be reconstructed
    /// from catalog data at simulation time, even if the player mutates gear or skills mid-battle.
    /// </summary>
    public class BattleSnapshot
    {
        /// <summary>
        /// The player's level at battle start.
        /// </summary>
        public required int Level { get; set; }

        /// <summary>
        /// The raw stat allocations (core attributes) at battle start.
        /// </summary>
        public required List<StatAllocation> StatAllocations { get; set; }

        /// <summary>
        /// The equipped items and their applied mod IDs at battle start.
        /// </summary>
        public required List<EquippedItemSnapshot> EquippedItems { get; set; }

        /// <summary>
        /// The IDs of the player's selected skills at battle start.
        /// </summary>
        public required List<int> SkillIds { get; set; }
    }

    /// <summary>
    /// Captures an equipped item and which mods were applied to it at battle start.
    /// </summary>
    public class EquippedItemSnapshot
    {
        /// <summary>
        /// The ID of the equipped item.
        /// </summary>
        public required int ItemId { get; set; }

        /// <summary>
        /// The IDs of item mods applied to this item at battle start.
        /// </summary>
        public required List<int> AppliedModIds { get; set; }
    }
}
