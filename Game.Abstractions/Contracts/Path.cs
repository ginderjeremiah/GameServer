namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a path: an ordered sequence of proficiencies (its tiers) plus the
    /// skills that contribute to it. The tiers are <see cref="Proficiency"/> records carrying this path's id;
    /// the contributions are a read projection (the identity save ignores them — they persist through the
    /// dedicated contributions setter, mirroring the other editors).</summary>
    public class Path : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        /// <summary>The geometric base of the per-tier contribution falloff (see the entity).</summary>
        public decimal FalloffBase { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).</summary>
        public DateTime? RetiredAt { get; set; }

        public required IEnumerable<SkillPathContribution> Contributions { get; set; }
    }
}
