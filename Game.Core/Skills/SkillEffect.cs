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
    }
}
