using Game.Core.Collections;

namespace Game.Core.Proficiencies
{
    /// <summary>
    /// Cycle detection over the proficiency prerequisite graph (spike #982 area D). A prerequisite edge points
    /// from a proficiency to a proficiency that must be maxed before it opens; a cycle (A needs B needs A)
    /// would soft-lock every node on it under the "open once prerequisites are maxed" rule, so it is rejected
    /// both at admin-authoring time (a clean failure before the write commits), as a build-time invariant on
    /// the cache snapshot (the backstop against a seed/migration mistake, mirroring the zero-based contiguity
    /// assertion), and as a Content Health lint check (defense-in-depth, since the admin save path already
    /// rejects a live cycle before it is persisted). Within-path order is implicit in the tier ordinals (tier
    /// N+1 requires tier N maxed) and inherently acyclic on its own, but a cross-path authored edge composed
    /// with that implicit chain can still close a cycle the authored edges alone never show — so
    /// <see cref="BuildGraph"/> folds each path's consecutive-tier edges into the same graph the authored
    /// prerequisites use before it is checked. This is a domain invariant (acyclicity of the prerequisite
    /// graph), so it lives here rather than in the data tier, reachable by both <c>Game.Application</c> and
    /// <c>Game.DataAccess</c>.
    /// </summary>
    public static class ProficiencyPrerequisiteGraph
    {
        /// <summary>
        /// Builds the combined prerequisite graph: <paramref name="prerequisites"/>'s authored edges plus one
        /// implicit edge per path from each tier to the tier immediately below it (ordered by
        /// <c>PathOrdinal</c>). <paramref name="tiers"/> must list every proficiency (id, path id, path
        /// ordinal) the graph should account for, regardless of whether it carries authored prerequisites.
        /// </summary>
        public static IReadOnlyDictionary<int, IReadOnlyList<int>> BuildGraph(
            IReadOnlyList<(int Id, int PathId, int PathOrdinal)> tiers,
            IReadOnlyDictionary<int, IReadOnlyList<int>> prerequisites)
        {
            var graph = prerequisites.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)[.. kv.Value]);

            foreach (var path in tiers.GroupBy(t => t.PathId))
            {
                var ordered = path.OrderBy(t => t.PathOrdinal).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    var currentId = ordered[i].Id;
                    var previousId = ordered[i - 1].Id;
                    var edges = graph.TryGetValue(currentId, out var existing) ? existing : [];
                    graph[currentId] = [.. edges, previousId];
                }
            }

            return graph;
        }

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
