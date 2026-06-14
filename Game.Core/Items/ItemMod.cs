using Game.Core.Attributes.Modifiers;
using Game.Core.Tags;

namespace Game.Core.Items
{
    /// <summary>
    /// Represents a modifier to an item. Shared, cached reference-data instance: structurally immutable
    /// (init-only properties, read-only collections) so the cached instance cannot be corrupted (#547).
    /// </summary>
    public class ItemMod
    {
        /// <summary>
        /// The unique identifier for the item mod.
        /// </summary>
        public required int Id { get; init; }

        /// <summary>
        /// The name of the item mod.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// A short description of the item mod.
        /// </summary>
        public required string Description { get; init; }

        /// <inheritdoc cref="EItemModType" />
        public EItemModType Type { get; init; }

        /// <inheritdoc cref="ERarity" />
        public required ERarity Rarity { get; init; }

        /// <summary>
        /// The attribute modifiers that the item mod applies.
        /// </summary>
        public required IReadOnlyList<AttributeModifier> Attributes { get; init; }

        /// <summary>
        /// The tags that describe the item mod.
        /// </summary>
        public required IReadOnlyList<Tag> Tags { get; init; }
    }
}
