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
    }
}
