namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// The skill-to-proficiency contribution join: using <see cref="SkillId"/> in a won battle feeds
    /// <see cref="ProficiencyId"/>'s XP, weighted by <see cref="Weight"/>. A skill may contribute to more than
    /// one proficiency (multi-contribution). Keyed by (skill, proficiency).
    /// </summary>
    public class SkillProficiency
    {
        public int SkillId { get; set; }
        public int ProficiencyId { get; set; }
        public decimal Weight { get; set; }

        public virtual Skill Skill { get => field ?? throw new NotLoadedException(nameof(Skill)); set; }
        public virtual Proficiency Proficiency { get => field ?? throw new NotLoadedException(nameof(Proficiency)); set; }
    }
}
