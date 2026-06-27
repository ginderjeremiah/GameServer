namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// Cycle detection over the proficiency prerequisite graph (spike #982 area D). A prerequisite edge points
    /// from a proficiency to a proficiency that must be maxed before it opens; a cycle (A needs B needs A)
    /// would soft-lock every node on it under the "open once prerequisites are maxed" rule, so it is rejected
    /// both at admin-authoring time (a clean failure before the write commits) and as a build-time invariant on
    /// the cache snapshot (the backstop against a seed/migration mistake, mirroring the zero-based contiguity
    /// assertion). Within-path order is implicit in the tier ordinals and inherently acyclic, so only the
    /// authored prerequisite edges are checked here.
    /// </summary>
    internal static class ProficiencyPrerequisiteGraph
    {
        /// <summary>
        /// Finds a cycle in the prerequisite graph if one exists. <paramref name="prerequisites"/> maps each
        /// proficiency id to the ids it depends on; an absent node is treated as having no prerequisites.
        /// Returns <c>true</c> and the cycle (a node sequence that returns to its start) when one is found.
        /// </summary>
        public static bool TryFindCycle(
            IReadOnlyDictionary<int, IReadOnlyList<int>> prerequisites,
            out IReadOnlyList<int> cycle)
        {
            return DirectedGraphCycleDetector.TryFindCycle(prerequisites, out cycle);
        }
    }
}
