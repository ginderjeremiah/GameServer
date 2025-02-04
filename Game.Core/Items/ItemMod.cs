using Game.Core.Attributes.Modifiers;
using Game.Core.Tags;

namespace Game.Core.Items
{
    /// <summary>
    /// Represents a modifier to an item.
    /// </summary>
    public class ItemMod
    {
        /// <summary>
        /// The name of the item mod.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Whether the item mod can be removed.
        /// </summary>
        public bool Removable { get; set; }

        /// <summary>
        /// A short description of the item mod.
        /// </summary>
        public required string Description { get; set; }

        /// <inheritdoc cref="EItemModType" />
        public EItemModType Type { get; set; }

        /// <summary>
        /// The attribute modifiers that the item mod applies.
        /// </summary>
        public required List<AttributeModifier> Attributes { get; set; }

        /// <summary>
        /// The tags that describe the item mod.
        /// </summary>
        public required List<Tag> Tags { get; set; }
    }
}
