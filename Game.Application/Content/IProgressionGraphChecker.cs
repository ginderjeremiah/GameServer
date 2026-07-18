using Game.Abstractions.Content;

namespace Game.Application.Content
{
    /// <summary>
    /// Walks the whole static content graph (spike #1390, decision 5) for cross-entity reachability breakage the
    /// per-entity admin saves can't catch — the dangling end of a broken reference lives on the *other* record,
    /// and a reference valid at save time can be broken later by retiring its target. The same checker powers a
    /// CI lint over the committed <c>content/*.json</c> export and a read-only admin Content Health view; both
    /// build a <see cref="ContentGraph"/> and run it through here.
    /// </summary>
    public interface IProgressionGraphChecker
    {
        /// <summary>Runs every reachability check over <paramref name="graph"/> and returns the findings in a
        /// deterministic order (by check, then entity). An empty list means the graph is healthy.</summary>
        IReadOnlyList<ContentGraphFinding> Check(ContentGraph graph);
    }
}
