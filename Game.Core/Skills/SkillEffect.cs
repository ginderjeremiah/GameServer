namespace Game.Core.Skills
{
    /// <summary>
    /// An authored effect that a skill applies when it fires — a timed attribute modifier on a target battler.
    /// Part of the shared, cached <see cref="Skill"/> graph and therefore structurally immutable (init-only)
    /// so the cached instance cannot be corrupted (#547).
    /// </summary>
    public class SkillEffect
    {
        public required int Id { get; init; }
        public required ESkillEffectTarget Target { get; init; }
        public required EAttribute AttributeId { get; init; }
        public required EModifierType ModifierType { get; init; }
        public required double Amount { get; init; }
        public required int DurationMs { get; init; }

        /// <summary>
        /// The caster attribute whose value scales this effect's magnitude. Together with
        /// <see cref="ScalingAmount"/> it adds <c>casterAttribute × ScalingAmount</c> to <see cref="Amount"/>
        /// when the effect fires, mirroring how a <see cref="DamageMultiplier"/> scales skill damage off the
        /// caster. A <see cref="ScalingAmount"/> of <c>0</c> means no scaling, leaving <see cref="Amount"/>
        /// unchanged.
        /// </summary>
        public required EAttribute ScalingAttributeId { get; init; }

        /// <summary>The per-point coefficient applied to the caster's <see cref="ScalingAttributeId"/> value.</summary>
        public required double ScalingAmount { get; init; }
    }
}
