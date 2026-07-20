using Game.Core.Collections;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// The skill-synthesis dependency graph (spike #1125): an edge points from a recipe's result skill to each
    /// of its input skills (the result <em>depends on</em> its inputs). A cycle means a skill is its own
    /// ancestor through chained recipes — you would need the skill to synthesize itself, so it could never be
    /// produced. Acyclicity is therefore exactly the reachability guarantee: every result is reachable from its
    /// base inputs. It is enforced both at admin-authoring time (a clean failure before the write commits) and
    /// as a build-time invariant on the cache snapshot (the backstop against a seed/migration mistake, mirroring
    /// the proficiency prerequisite graph and the zero-based contiguity assertion). Retired recipes are excluded
    /// — a retired recipe is inert (out of circulation), so it never constrains reachability.
    /// </summary>
    internal static class SkillRecipeGraph
    {
        /// <summary>
        /// Builds the result-skill → input-skills dependency graph from the given recipes (each a result skill
        /// id and its input skill ids). When several recipes produce the same result skill, their inputs are
        /// unioned, since any of them makes the result depend on those inputs.
        /// </summary>
        public static IReadOnlyDictionary<int, IReadOnlyList<int>> BuildDependencyGraph(
            IEnumerable<(int ResultSkillId, IReadOnlyList<int> InputSkillIds)> recipes)
        {
            var dependencies = new Dictionary<int, List<int>>();
            foreach (var (resultSkillId, inputSkillIds) in recipes)
            {
                if (!dependencies.TryGetValue(resultSkillId, out var inputs))
                {
                    inputs = [];
                    dependencies[resultSkillId] = inputs;
                }

                inputs.AddRange(inputSkillIds);
            }

            return dependencies.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)kv.Value);
        }

        /// <summary>
        /// Finds a cycle in the dependency graph if one exists. Returns <c>true</c> and the cycle (a skill-id
        /// sequence that returns to its start) when a skill is its own ancestor through chained recipes.
        /// </summary>
        public static bool TryFindCycle(
            IReadOnlyDictionary<int, IReadOnlyList<int>> dependencies,
            out IReadOnlyList<int> cycle)
        {
            return DirectedGraphCycleDetector.TryFindCycle(dependencies, out cycle);
        }
    }
}
