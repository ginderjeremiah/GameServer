namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// One issue surfaced by the whole-graph content lint, anchored to the record that carries the broken
    /// reference (<see cref="EntityKind"/> + <see cref="EntityId"/>) so an admin surface can point at it. The
    /// dangling end of a broken cross-reference lives on the *other* record — exactly what a per-entity save
    /// can't see — so a finding names the record holding the reference, not the missing target.
    /// </summary>
    public class ContentHealthFinding
    {
        /// <summary>How serious the finding is (gates the CI lint build only for <see cref="EContentHealthSeverity.Error"/>).</summary>
        public EContentHealthSeverity Severity { get; set; }

        /// <summary>Stable check-category key (e.g. <c>ZoneBoss</c>, <c>OrphanSkill</c>) for grouping in the view.</summary>
        public required string Check { get; set; }

        /// <summary>The kind of the offending record (e.g. <c>Zone</c>, <c>Skill</c>).</summary>
        public required string EntityKind { get; set; }

        /// <summary>The id of the offending record within its set.</summary>
        public int EntityId { get; set; }

        /// <summary>Human-readable description of the issue.</summary>
        public required string Message { get; set; }
    }
}
