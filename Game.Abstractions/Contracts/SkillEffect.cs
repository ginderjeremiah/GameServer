using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a skill effect in the reference-data catalogue.</summary>
    public class SkillEffect : IModel
    {
        public int Id { get; set; }
        public ESkillEffectTarget Target { get; set; }
        public EAttribute AttributeId { get; set; }
        public EModifierType ModifierTypeId { get; set; }
        public decimal Amount { get; set; }
        public int DurationMs { get; set; }

        /// <summary>The caster attribute whose value scales this effect's magnitude (see <see cref="ScalingAmount"/>).</summary>
        public EAttribute ScalingAttributeId { get; set; }

        /// <summary>The per-point coefficient applied to the caster's scaling attribute; <c>0</c> means no scaling.</summary>
        public decimal ScalingAmount { get; set; }
    }
}
