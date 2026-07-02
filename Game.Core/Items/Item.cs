using Game.Core.Attributes.Modifiers;

namespace Game.Core.Items
{
    /// <summary>
    /// Represents an item that can be equipped. Shared, cached reference-data instance: structurally
    /// immutable (init-only properties, read-only collections) so it can be safely returned from the
    /// reference cache to every player without a consumer corrupting the shared graph (#547).
    /// </summary>
    public class Item
    {
        /// <summary>
        /// The unique identifier of the item.
        /// </summary>
        public required int Id { get; init; }

        /// <summary>
        /// The name of the item.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// A short description of the item.
        /// </summary>
        public required string Description { get; init; }

        /// <inheritdoc cref="EItemCategory" />
        public required EItemCategory Category { get; init; }

        /// <inheritdoc cref="ERarity" />
        public required ERarity Rarity { get; init; }

        /// <summary>
        /// The id of the skill this item grants while equipped, or null for none. Stored as an id only
        /// (not the resolved <see cref="Skills.Skill"/>) so the item cache stays independent of the skill
        /// cache; the battle assembly resolves it against the skill catalog at simulation time.
        /// </summary>
        public int? GrantedSkillId { get; init; }

        /// <summary>
        /// The <see cref="EDamageType"/> leaf this weapon deals, or null for a non-weapon item — any leaf is
        /// valid, so a caster weapon can declare its element (e.g. Fire) rather than a martial leaf. Only
        /// meaningful on a <see cref="EItemCategory.Weapon"/> item; the weapon-match loadout gate resolves an
        /// equipped weapon's type as <c>weapon?.WeaponType ?? Unarmed</c> (an empty slot is the virtual fists).
        /// </summary>
        public EDamageType? WeaponType { get; init; }

        /// <summary>
        /// The id of the proficiency that gates equipping this item, or null when the item is ungated.
        /// Stored as an id only (not the resolved proficiency) so the item cache stays independent of the
        /// proficiency reference data; the equip gate is evaluated against the player's proficiency levels.
        /// </summary>
        public int? RequiredProficiencyId { get; init; }

        /// <summary>
        /// The minimum level the player must have reached in <see cref="RequiredProficiencyId"/> to equip
        /// this item. Only meaningful when <see cref="RequiredProficiencyId"/> is set.
        /// </summary>
        public int RequiredProficiencyLevel { get; init; }

        /// <summary>
        /// The attribute modifiers that the item applies.
        /// </summary>
        public required IReadOnlyList<AttributeModifier> Attributes { get; init; }

        /// <summary>
        /// The mod slots that the item has.
        /// </summary>
        public required IReadOnlyList<ItemModSlot> ModSlots { get; init; }

        /// <summary>
        /// The attribute modifiers this item contributes when equipped: its own <see cref="Attributes"/>
        /// plus the attributes of the given <paramref name="appliedMods"/>. Single source of truth for the
        /// item + applied-mods composition rule, shared by the live
        /// <see cref="Players.Inventories.Inventory.GetEquippedAttributeModifiers"/> path and the
        /// battle-snapshot reconstruction (<see cref="Battle.BattleSnapshot.ToBattler"/>).
        /// </summary>
        public IEnumerable<AttributeModifier> GetAttributeModifiers(IEnumerable<ItemMod> appliedMods)
        {
            return Attributes.Concat(appliedMods.SelectMany(mod => mod.Attributes));
        }

        /// <summary>
        /// Whether a player meets this item's proficiency gate given their current per-proficiency levels
        /// (a missing entry is level 0 — the "never trained" state). An ungated item
        /// (<see cref="RequiredProficiencyId"/> is null) is always equippable; a gated item requires the
        /// player's level in <see cref="RequiredProficiencyId"/> to be at least
        /// <see cref="RequiredProficiencyLevel"/>. Mirrors <see cref="Game.Core.Zones.Zone.IsUnlocked"/>.
        /// </summary>
        public bool MeetsProficiencyRequirement(IReadOnlyDictionary<int, int> proficiencyLevels)
        {
            return RequiredProficiencyId is not int proficiencyId
                || proficiencyLevels.GetValueOrDefault(proficiencyId) >= RequiredProficiencyLevel;
        }
    }
}
