namespace Game.Core.Skills
{
    /// <summary>
    /// An authored effect that a skill applies when it fires — a timed attribute modifier on a target battler.
    /// Not consumed by battle until the runtime issue (#333).
    /// </summary>
    public class SkillEffect
    {
        public required int Id { get; set; }
        public required ESkillEffectTarget Target { get; set; }
        public required EAttribute AttributeId { get; set; }
        public required EModifierType ModifierType { get; set; }
        public required double Amount { get; set; }
        public required int DurationMs { get; set; }
    }
}
