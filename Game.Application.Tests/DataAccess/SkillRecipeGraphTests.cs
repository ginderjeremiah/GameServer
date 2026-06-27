using Game.DataAccess.Repositories.Caching;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// The skill-synthesis dependency graph (spike #1125): an edge points from a recipe's result skill to each
    /// of its input skills, and a cycle means a skill is its own ancestor through chained recipes (unreachable).
    /// Acyclicity must be caught both at admin-authoring time and as a build-time cache invariant. Pure graph
    /// algorithm — no DB.
    /// </summary>
    public class SkillRecipeGraphTests
    {
        [Fact]
        public void AcyclicChain_HasNoCycle()
        {
            // C is synthesized from A + B; E from C + D — a DAG, no cycle.
            var graph = SkillRecipeGraph.BuildDependencyGraph(
            [
                (ResultSkillId: 2, InputSkillIds: Inputs(0, 1)),
                (ResultSkillId: 4, InputSkillIds: Inputs(2, 3)),
            ]);

            Assert.False(SkillRecipeGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void TwoRecipeCycle_IsDetected()
        {
            // C depends on A; A depends on C — you would need C to make A and A to make C.
            var graph = SkillRecipeGraph.BuildDependencyGraph(
            [
                (ResultSkillId: 2, InputSkillIds: Inputs(0)),
                (ResultSkillId: 0, InputSkillIds: Inputs(2)),
            ]);

            Assert.True(SkillRecipeGraph.TryFindCycle(graph, out var cycle));
            // The cycle closes back on its starting node.
            Assert.Equal(cycle[0], cycle[^1]);
        }

        [Fact]
        public void LongerCycle_IsDetected()
        {
            var graph = SkillRecipeGraph.BuildDependencyGraph(
            [
                (ResultSkillId: 0, InputSkillIds: Inputs(1)),
                (ResultSkillId: 1, InputSkillIds: Inputs(2)),
                (ResultSkillId: 2, InputSkillIds: Inputs(0)),
            ]);

            Assert.True(SkillRecipeGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void SelfLoop_IsDetected()
        {
            // A recipe whose result is also its input (admin rejects this separately, but the graph catches it).
            var graph = SkillRecipeGraph.BuildDependencyGraph([(ResultSkillId: 0, InputSkillIds: Inputs(0))]);

            Assert.True(SkillRecipeGraph.TryFindCycle(graph, out _));
        }

        [Fact]
        public void EmptyGraph_HasNoCycle()
        {
            Assert.False(SkillRecipeGraph.TryFindCycle(SkillRecipeGraph.BuildDependencyGraph([]), out _));
        }

        [Fact]
        public void BuildDependencyGraph_UnionsInputsForTheSameResultSkill()
        {
            // Two recipes both produce skill 3; the result depends on the union of their inputs.
            var graph = SkillRecipeGraph.BuildDependencyGraph(
            [
                (ResultSkillId: 3, InputSkillIds: Inputs(0, 1)),
                (ResultSkillId: 3, InputSkillIds: Inputs(2)),
            ]);

            Assert.Equal([0, 1, 2], graph[3].OrderBy(id => id));
        }

        private static IReadOnlyList<int> Inputs(params int[] ids) => ids;
    }
}
