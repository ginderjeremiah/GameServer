using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a skill's per-attribute damage multiplier.</summary>
    public class AttributeMultiplier : IModel
    {
        public EAttribute AttributeId { get; set; }
        public decimal Multiplier { get; set; }
    }
}
