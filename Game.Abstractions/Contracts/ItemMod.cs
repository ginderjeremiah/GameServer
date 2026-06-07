using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an item modifier in the reference-data catalogue.</summary>
    public class ItemMod : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EItemModType ItemModTypeId { get; set; }
        public ERarity RarityId { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }
        public required IEnumerable<int> Tags { get; set; }
    }
}
