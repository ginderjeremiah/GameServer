using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a per-attribute base/per-level distribution — an enemy's attribute scaling
    /// and a class's locked-base fingerprint (#1126) both use this shape.</summary>
    public class AttributeDistribution : IModel
    {
        public EAttribute AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }
    }
}
