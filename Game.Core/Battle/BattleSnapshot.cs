using Game.Core.Attributes;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Skills;

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

        /// <summary>
        /// Captures a player's current battle-relevant state as a minimal ID-based snapshot. The
        /// projection lives in the domain alongside <see cref="ToBattler"/> so the capture/reconstruct
        /// pair stays a single, self-consistent unit.
        /// </summary>
        public static BattleSnapshot FromPlayer(Player player)
        {
            var equippedItems = player.Inventory.EquipmentSlots
                .SelectNotNull(slot => slot.ItemId)
                .Select(itemId =>
                {
                    // Applied mods live on the player's UnlockedItemSlot, so capture their IDs from there.
                    // An equipped item must always have a matching unlocked entry (TryEquipItem enforces
                    // this), so a missing entry means the inventory invariant is broken. Fail loudly rather
                    // than silently capturing the item without its mods: this snapshot is the anti-cheat
                    // parity surface, and a quiet capture would later validate a replay against weaker
                    // attributes than the client simulated, failing legitimate victories with no signal.
                    var unlocked = player.Inventory.GetUnlockedItem(itemId)
                        ?? throw new InvalidOperationException(
                            $"Equipped item {itemId} has no matching entry in the player's unlocked items.");

                    var modIds = unlocked.AppliedMods
                        .Select(m => m.ItemModId)
                        .ToList();

                    return new EquippedItemSnapshot
                    {
                        ItemId = itemId,
                        AppliedModIds = modIds,
                    };
                })
                .ToList();

            return new BattleSnapshot
            {
                Level = player.Level,
                // Copy each allocation so a later in-place stat reallocation on the live player cannot
                // retroactively mutate this snapshot, consistent with the other projected fields.
                StatAllocations = player.StatPoints.StatAllocations.Select(allocation => allocation.Copy()).ToList(),
                EquippedItems = equippedItems,
                SkillIds = player.SelectedSkills.Select(s => s.Id).ToList(),
            };
        }

        /// <summary>
        /// Reconstructs the player's <see cref="Battler"/> from this snapshot, resolving the captured IDs
        /// against the in-memory catalogs via the supplied resolvers. The same modifier-composition rules the
        /// live <see cref="Player.GetAllModifiers"/> path uses are reused here (<see cref="StatAllocation.ToModifier"/>
        /// and <see cref="Item.GetAttributeModifiers"/>), so a battle validated from a snapshot is guaranteed to
        /// match the player's live attributes — the frontend/backend battle-parity guarantee. The caller provides
        /// the resolvers so the domain stays independent of the data-access layer that owns catalog lookups,
        /// mirroring <see cref="BattleFactory"/>'s enemy resolver.
        /// </summary>
        public Battler ToBattler(Func<int, Item> resolveItem, Func<int, ItemMod> resolveMod, Func<int, Skill> resolveSkill)
        {
            var modifiers = StatAllocations.Select(allocation => allocation.ToModifier())
                .Concat(EquippedItems.SelectMany(equipped =>
                    resolveItem(equipped.ItemId)
                        .GetAttributeModifiers(equipped.AppliedModIds.Select(resolveMod))));

            var attributes = new AttributeCollection(modifiers);
            var skills = SkillIds.Select(resolveSkill);

            return new Battler(attributes, skills, Level);
        }
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
