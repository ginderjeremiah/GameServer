using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an enemy's per-attribute base/per-level distribution.</summary>
    public class AttributeDistribution : IModel
    {
        public EAttribute AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }
    }
}
