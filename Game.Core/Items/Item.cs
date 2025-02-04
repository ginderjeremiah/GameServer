using Game.Core.Attributes.Modifiers;
using Game.Core.Tags;

namespace Game.Core.Items
{
    /// <summary>
    /// Represents an item that can be equipped.
    /// </summary>
    public class Item
    {
        /// <summary>
        /// The unique identifier of the item.
        /// </summary>
        public required int Id { get; set; }

        /// <summary>
        /// The name of the item.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// A short description of the item.
        /// </summary>
        public required string Description { get; set; }

        /// <inheritdoc cref="EItemCategory" />
        public required EItemCategory Category { get; set; }

        /// <summary>
        /// The attribute modifiers that the item applies.
        /// </summary>
        public required List<AttributeModifier> Attributes { get; set; }

        /// <summary>
        /// The mod slots that the item has.
        /// </summary>
        public required List<ItemModSlot> ModSlots { get; set; }

        /// <summary>
        /// The tags that describe the item.
        /// </summary>
        public required List<Tag> Tags { get; set; }
    }
}
