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
            // Standard three-colour DFS: a back-edge to a node still on the current stack is a cycle.
            var visited = new HashSet<int>();
            var onStack = new HashSet<int>();
            var stack = new List<int>();

            foreach (var node in prerequisites.Keys)
            {
                if (Visit(node, prerequisites, visited, onStack, stack, out cycle))
                {
                    return true;
                }
            }

            cycle = [];
            return false;
        }

        private static bool Visit(
            int node,
            IReadOnlyDictionary<int, IReadOnlyList<int>> prerequisites,
            HashSet<int> visited,
            HashSet<int> onStack,
            List<int> stack,
            out IReadOnlyList<int> cycle)
        {
            if (!visited.Add(node))
            {
                cycle = [];
                return false;
            }

            onStack.Add(node);
            stack.Add(node);

            if (prerequisites.TryGetValue(node, out var edges))
            {
                foreach (var next in edges)
                {
                    if (onStack.Contains(next))
                    {
                        // Slice the current stack from the revisited node to close the cycle for the message.
                        var start = stack.IndexOf(next);
                        cycle = [.. stack.Skip(start), next];
                        return true;
                    }

                    if (!visited.Contains(next)
                        && Visit(next, prerequisites, visited, onStack, stack, out cycle))
                    {
                        return true;
                    }
                }
            }

            onStack.Remove(node);
            stack.RemoveAt(stack.Count - 1);
            cycle = [];
            return false;
        }
    }
}
