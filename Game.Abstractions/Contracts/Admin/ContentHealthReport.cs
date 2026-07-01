namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// A read-only snapshot of the whole-graph content lint over the live reference caches: the findings plus
    /// their error/warning counts, powering the admin "Content Health" view (spike #1390, decision 5). Computing
    /// it never mutates content — it walks the same static graph the CI lint checks, but against the running
    /// server's caches rather than the committed <c>content/*.json</c> export.
    /// </summary>
    public class ContentHealthReport : IModel
    {
        /// <summary>Number of <see cref="EContentHealthSeverity.Error"/> findings.</summary>
        public int ErrorCount { get; set; }

        /// <summary>Number of <see cref="EContentHealthSeverity.Warning"/> findings.</summary>
        public int WarningCount { get; set; }

        /// <summary>Every finding, in the checker's deterministic order (by check, then entity).</summary>
        public required IReadOnlyList<ContentHealthFinding> Findings { get; set; }
    }
}
