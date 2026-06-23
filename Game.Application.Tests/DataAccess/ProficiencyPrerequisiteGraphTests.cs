using Game.DataAccess.Repositories.Caching;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Cycle detection over the proficiency prerequisite graph (spike #982 area D): a cycle would soft-lock
    /// every node on it under the "open once prerequisites are maxed" rule, so it must be caught both at
    /// admin-authoring time and as a build-time cache invariant. Pure graph algorithm — no DB.
    /// </summary>
    public class ProficiencyPrerequisiteGraphTests
    {
        [Fact]
        public void Acyclic_Diamond_HasNoCycle()
        {
            // D needs B and C; both need A — a DAG with a shared root, no cycle.
            var graph = Graph((3, [1, 2]), (1, [0]), (2, [0]), (0, []));
            Assert.False(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void TwoNodeCycle_IsDetected()
        {
            var graph = Graph((0, [1]), (1, [0]));
            Assert.True(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out var cycle));
            // The cycle closes back on its starting node.
            Assert.Equal(cycle[0], cycle[^1]);
        }

        [Fact]
        public void ThreeNodeCycle_IsDetected()
        {
            var graph = Graph((0, [1]), (1, [2]), (2, [0]));
            Assert.True(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void SelfLoop_IsDetected()
        {
            var graph = Graph((0, [0]));
            Assert.True(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void EmptyGraph_HasNoCycle()
        {
            Assert.False(ProficiencyPrerequisiteGraph.TryFindCycle(Graph(), out _));
        }

        [Fact]
        public void EdgeToAnAbsentNode_IsTreatedAsALeaf()
        {
            // A prerequisite id with no entry of its own (e.g. a leaf root) is a dead end, not a cycle.
            var graph = Graph((1, [0]));
            Assert.False(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out _));
        }

        private static Dictionary<int, IReadOnlyList<int>> Graph(params (int Node, int[] Edges)[] edges) =>
            edges.ToDictionary(e => e.Node, e => (IReadOnlyList<int>)e.Edges);
    }
}
