namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// The skill-to-path contribution join: using <see cref="SkillId"/> in a won battle feeds the XP of the
    /// path it contributes to (<see cref="PathId"/>), weighted by <see cref="Weight"/> and decaying with the
    /// distance from its <see cref="HomeTier"/> (see <see cref="Path.FalloffBase"/>). A skill may contribute
    /// to more than one path — a genuine cross-school skill. Keyed by (skill, path).
    /// </summary>
    public class SkillPathContribution
    {
        public int SkillId { get; set; }
        public int PathId { get; set; }

        /// <summary>The path tier (a <see cref="Proficiency.PathOrdinal"/>) this skill is native to; its pull
        /// on a deeper tier falls off with the distance from it.</summary>
        public int HomeTier { get; set; }
        public decimal Weight { get; set; }

        public virtual Skill Skill { get => field ?? throw new NotLoadedException(nameof(Skill)); set; }
        public virtual Path Path { get => field ?? throw new NotLoadedException(nameof(Path)); set; }
    }
}
