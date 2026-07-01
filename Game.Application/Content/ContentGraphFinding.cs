namespace Game.Application.Content
{
    /// <summary>How serious a <see cref="ContentGraphFinding"/> is.</summary>
    public enum ContentGraphSeverity
    {
        /// <summary>Unreachable / dead content that the runtime tolerates (e.g. an orphan skill no source grants,
        /// a live zone with no enemies the player is simply relocated out of). Surfaced, but does not gate a build.</summary>
        Warning = 0,

        /// <summary>A genuine break the runtime does not paper over: a dangling reference, or live content wedged
        /// into a permanently unusable state (a zone gated by a retired challenge, an item gated by a frozen
        /// proficiency). Gates the content-lint CI build.</summary>
        Error = 1,
    }

    /// <summary>
    /// One issue found by the <see cref="IProgressionGraphChecker">progression-graph lint</see>. Carries the
    /// severity, a stable <see cref="Check"/> category key (for grouping / suppression), and the offending
    /// entity (<see cref="EntityKind"/> + <see cref="EntityId"/>) so an admin surface can anchor the warning to
    /// a record. The dangling end of a broken cross-reference lives on the *other* record, which is exactly what
    /// a per-entity save can't see — so a finding names the record that carries the broken reference.
    /// </summary>
    public sealed record ContentGraphFinding(
        ContentGraphSeverity Severity,
        string Check,
        string EntityKind,
        int EntityId,
        string Message)
    {
        public override string ToString() => $"[{Severity}] {EntityKind} {EntityId} ({Check}): {Message}";
    }
}
