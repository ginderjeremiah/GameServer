using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an item in the reference-data catalogue.</summary>
    public class Item : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EItemCategory ItemCategoryId { get; set; }
        public ERarity RarityId { get; set; }
        public required string IconPath { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }
        public required IEnumerable<ItemModSlot> ModSlots { get; set; }
        public required IEnumerable<int> Tags { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
