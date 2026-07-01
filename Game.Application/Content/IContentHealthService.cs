using Game.Abstractions.Contracts.Admin;

namespace Game.Application.Content
{
    /// <summary>
    /// Runs the whole-graph content lint (<see cref="IProgressionGraphChecker"/>) over the server's live
    /// reference caches and projects the findings onto the admin <see cref="ContentHealthReport"/> contract.
    /// The read-only counterpart to the CI drift lint: same checker, but against the running caches instead of
    /// the committed export, so an admin can see broken / unreachable content live (spike #1390, decision 5).
    /// </summary>
    public interface IContentHealthService
    {
        /// <summary>Builds a <see cref="ContentGraph"/> from the reference caches, checks it, and returns the
        /// report. Purely read-only — it touches no write path and mutates nothing.</summary>
        ContentHealthReport GetReport();
    }
}
