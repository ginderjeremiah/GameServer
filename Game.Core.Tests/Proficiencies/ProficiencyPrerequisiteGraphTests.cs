using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Proficiencies
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

        [Fact]
        public void BuildGraph_WithinPathTierChain_AddsNoCycleOnItsOwn()
        {
            // Three tiers of one path (ordinals 0, 1, 2) and no authored prerequisites: the implicit
            // tier-N+1-needs-tier-N chain is a straight line, not a cycle.
            var tiers = Tiers((0, path: 0, ordinal: 0), (1, path: 0, ordinal: 1), (2, path: 0, ordinal: 2));
            var graph = ProficiencyPrerequisiteGraph.BuildGraph(tiers, Graph());
            Assert.False(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void BuildGraph_AuthoredEdgeBackToAnEarlierTierOnTheSamePath_IsDetected()
        {
            // Tier 0 of a path authoring a prerequisite on tier 1 of the same path deadlocks against the
            // implicit tier-1-needs-tier-0 edge — a same-path cycle composed from one authored + one implicit edge.
            var tiers = Tiers((0, path: 0, ordinal: 0), (1, path: 0, ordinal: 1));
            var graph = ProficiencyPrerequisiteGraph.BuildGraph(tiers, Graph((0, [1])));
            Assert.True(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void BuildGraph_CrossPathEdgesComposedWithImplicitTierOrdering_IsDetected()
        {
            // The #2144 scenario: Path A tier 0 (id 0) gates on Path B tier 2 (id 3); Path B tier 0 (id 1)
            // gates on Path A tier 0 (id 0). Neither authored edge revisits its own path, so a check over the
            // authored edges alone sees no cycle. But Path B's implicit chain (id 3 needs id 2 needs id 1)
            // composes with the two authored edges into 0 -> 3 -> 2 -> 1 -> 0.
            var tiers = Tiers(
                (0, path: 0, ordinal: 0), // Path A tier 0
                (1, path: 1, ordinal: 0), // Path B tier 0
                (2, path: 1, ordinal: 1), // Path B tier 1
                (3, path: 1, ordinal: 2)); // Path B tier 2
            var authored = Graph((0, [3]), (1, [0]));
            Assert.False(ProficiencyPrerequisiteGraph.TryFindCycle(authored, out _));

            var graph = ProficiencyPrerequisiteGraph.BuildGraph(tiers, authored);
            Assert.True(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out var cycle));
            Assert.Equal(cycle[0], cycle[^1]);
        }

        [Fact]
        public void BuildGraph_TiersOnDifferentPaths_AddNoImplicitEdgeBetweenThem()
        {
            // Two single-tier paths (ordinal 0 each) never chain to one another regardless of id order.
            var tiers = Tiers((5, path: 0, ordinal: 0), (2, path: 1, ordinal: 0));
            var graph = ProficiencyPrerequisiteGraph.BuildGraph(tiers, Graph());
            Assert.False(ProficiencyPrerequisiteGraph.TryFindCycle(graph, out _));
            Assert.False(graph.ContainsKey(5));
            Assert.False(graph.ContainsKey(2));
        }

        private static Dictionary<int, IReadOnlyList<int>> Graph(params (int Node, int[] Edges)[] edges) =>
            edges.ToDictionary(e => e.Node, e => (IReadOnlyList<int>)e.Edges);

        private static List<(int Id, int PathId, int PathOrdinal)> Tiers(params (int Id, int path, int ordinal)[] tiers) =>
            tiers.Select(t => (t.Id, t.path, t.ordinal)).ToList();
    }
}
