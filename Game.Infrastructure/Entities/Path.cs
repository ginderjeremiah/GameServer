namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A path: an ordered sequence of proficiencies sharing an identity arc (e.g. Fire Magic → Inferno
    /// Magic) and the first-class unit a skill contributes to (see the proficiency-system spike,
    /// <c>docs/spikes/982-proficiency-system.md</c> → Paths). Static, authored reference data with a
    /// zero-based identity. Its tiers are the <see cref="Proficiency"/> rows carrying this path's id, ordered
    /// by <see cref="Proficiency.PathOrdinal"/>; a standalone proficiency is just a path of length one.
    /// </summary>
    public class Path : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        /// <summary>
        /// The geometric base of the per-tier contribution falloff: a skill homed at one tier contributes to
        /// a deeper tier at <c>FalloffBase^tierDistance</c> (so <c>1</c> = no falloff; <c>0.3</c> = each
        /// deeper tier multiplies the weight by 0.3). Authored here; consumed by the XP-routing sub-issue
        /// (#1161).
        /// </summary>
        public decimal FalloffBase { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual List<Proficiency> Proficiencies { get => field ?? throw new NotLoadedException(nameof(Proficiencies)); set; }
        public virtual List<SkillPathContribution> SkillContributions { get => field ?? throw new NotLoadedException(nameof(SkillContributions)); set; }
    }
}
