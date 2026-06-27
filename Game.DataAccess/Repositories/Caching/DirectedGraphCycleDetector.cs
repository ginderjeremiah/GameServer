namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// Generic cycle detection over a directed graph expressed as an adjacency map (node → the nodes it points
    /// to). Shared by the reference-data graphs that must be acyclic — currently the skill-recipe dependency
    /// graph (<see cref="SkillRecipeGraph"/>) — both as an admin-authoring guard and as a build-time cache invariant.
    /// </summary>
    internal static class DirectedGraphCycleDetector
    {
        /// <summary>
        /// Finds a cycle in <paramref name="edges"/> if one exists. An absent node is treated as a leaf (no
        /// outgoing edges). Returns <c>true</c> and the cycle (a node sequence that returns to its start) when
        /// one is found, else <c>false</c> and an empty list.
        /// </summary>
        public static bool TryFindCycle<TNode>(
            IReadOnlyDictionary<TNode, IReadOnlyList<TNode>> edges,
            out IReadOnlyList<TNode> cycle) where TNode : notnull
        {
            // Standard three-colour DFS: a back-edge to a node still on the current stack is a cycle.
            var visited = new HashSet<TNode>();
            var onStack = new HashSet<TNode>();
            var stack = new List<TNode>();

            foreach (var node in edges.Keys)
            {
                if (Visit(node, edges, visited, onStack, stack, out cycle))
                {
                    return true;
                }
            }

            cycle = [];
            return false;
        }

        private static bool Visit<TNode>(
            TNode node,
            IReadOnlyDictionary<TNode, IReadOnlyList<TNode>> edges,
            HashSet<TNode> visited,
            HashSet<TNode> onStack,
            List<TNode> stack,
            out IReadOnlyList<TNode> cycle) where TNode : notnull
        {
            if (!visited.Add(node))
            {
                cycle = [];
                return false;
            }

            onStack.Add(node);
            stack.Add(node);

            if (edges.TryGetValue(node, out var next))
            {
                foreach (var neighbour in next)
                {
                    if (onStack.Contains(neighbour))
                    {
                        // Slice the current stack from the revisited node to close the cycle for the message.
                        var start = stack.IndexOf(neighbour);
                        cycle = [.. stack.Skip(start), neighbour];
                        return true;
                    }

                    if (!visited.Contains(neighbour)
                        && Visit(neighbour, edges, visited, onStack, stack, out cycle))
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
