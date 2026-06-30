namespace Game.Infrastructure.Entities
{
    /// <summary>One weighted slice of a skill's direct-hit damage (spike #1343). Keyed by
    /// <c>(SkillId, DamageType)</c> — a skill carries at most one portion per leaf type — the same EF
    /// child-table shape as <see cref="SkillDamageMultiplier"/>. <see cref="DamageType"/> is a plain
    /// enum-as-int column (no FK; <see cref="Core.EDamageType"/> is intrinsic).</summary>
    public class SkillDamagePortion
    {
        public int SkillId { get; set; }
        public int DamageType { get; set; }
        public decimal Weight { get; set; }

        public virtual Skill Skill { get => field ?? throw new NotLoadedException(nameof(Skill)); set; }
    }
}
