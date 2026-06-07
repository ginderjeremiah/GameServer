using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Players;

namespace Game.Application.Services
{
    /// <summary>
    /// Creates <see cref="BattleSnapshot"/> instances from player state and
    /// reconstructs <see cref="Battler"/> instances from snapshots using catalog data.
    /// </summary>
    public class BattleSnapshotService(IItems items, IItemMods itemMods, ISkills skills)
    {
        private readonly IItems _items = items;
        private readonly IItemMods _itemMods = itemMods;
        private readonly ISkills _skills = skills;

        /// <summary>
        /// Captures the player's current battle-relevant state as a minimal ID-based snapshot.
        /// </summary>
        public BattleSnapshot CreateSnapshot(Player player)
        {
            var equippedItems = player.Inventory.EquipmentSlots
                .SelectNotNull(slot => slot.ItemId)
                .Select(itemId =>
                {
                    // Find the UnlockedItemSlot for this equipped item to get applied mod IDs.
                    var unlocked = player.Inventory.UnlockedItems
                        .FirstOrDefault(u => u.ItemId == itemId);

                    var modIds = unlocked?.AppliedMods
                        .Select(m => m.ItemModId)
                        .ToList() ?? [];

                    return new EquippedItemSnapshot
                    {
                        ItemId = itemId,
                        AppliedModIds = modIds,
                    };
                })
                .ToList();

            var skillIds = player.SelectedSkills
                .Select(s => s.Id)
                .ToList();

            return new BattleSnapshot
            {
                Level = player.Level,
                StatAllocations = player.StatPoints.StatAllocations,
                EquippedItems = equippedItems,
                SkillIds = skillIds,
            };
        }

        /// <summary>
        /// Reconstructs a <see cref="Battler"/> from a snapshot by resolving IDs against
        /// in-memory catalogs. This ensures the simulation uses the player's state at battle
        /// start, regardless of any mutations that occurred since.
        /// </summary>
        public Battler CreateFromSnapshot(BattleSnapshot snapshot)
        {
            var modifiers = new List<AttributeModifier>();

            // 1. Stat allocations → Additive modifiers (same as PlayerStatPoints.ToAttributeModifiers)
            foreach (var alloc in snapshot.StatAllocations)
            {
                modifiers.Add(new AttributeModifier
                {
                    Attribute = alloc.Attribute,
                    Amount = alloc.Amount,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.PlayerStatPoints,
                });
            }

            // 2. Equipped items → item attribute modifiers + applied mod attribute modifiers
            foreach (var equip in snapshot.EquippedItems)
            {
                var item = _items.GetItem(equip.ItemId);
                modifiers.AddRange(item.Attributes);

                foreach (var modId in equip.AppliedModIds)
                {
                    var mod = _itemMods.GetItemMod(modId);
                    modifiers.AddRange(mod.Attributes);
                }
            }

            var attributes = new AttributeCollection(modifiers);

            // 3. Resolve skills from catalog by ID
            var resolvedSkills = snapshot.SkillIds
                .Select(id => _skills.GetSkill(id))
                .ToList();

            return new Battler(attributes, resolvedSkills, snapshot.Level);
        }
    }
}
