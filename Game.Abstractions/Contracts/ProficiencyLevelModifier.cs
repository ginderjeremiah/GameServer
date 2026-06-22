using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a proficiency's per-level attribute bonus (granted on reaching the level).</summary>
    public class ProficiencyLevelModifier : IModel
    {
        public int Level { get; set; }
        public EAttribute AttributeId { get; set; }
        public EModifierType ModifierTypeId { get; set; }
        public decimal Amount { get; set; }
    }
}
