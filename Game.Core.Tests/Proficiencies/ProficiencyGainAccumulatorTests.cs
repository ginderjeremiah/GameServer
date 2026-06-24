using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The offline away-window fold (spike #982 decision 9): per-battle <see cref="ProficiencyAccrualResult"/>s
    /// collapse into one aggregate for the welcome-back summary — XP summed per proficiency, level/residual XP
    /// taken from the final touch, milestones and granted skills unioned, opened nodes deduped — all in
    /// first-seen order so the summary is deterministic.
    /// </summary>
    public class ProficiencyGainAccumulatorTests
    {
        private static ProficiencyAccrualResult Battle(params ProficiencyXpResult[] results) =>
            new(results, []);

        [Fact]
        public void Build_NoBattlesAdded_IsEmpty()
        {
            var aggregate = new ProficiencyGainAccumulator().Build();

            Assert.Empty(aggregate.Results);
            Assert.Empty(aggregate.Opened);
        }

        [Fact]
        public void Add_SumsXp_AndKeepsTheFinalLevelAndResidualXp()
        {
            var acc = new ProficiencyGainAccumulator();
            acc.Add(Battle(new ProficiencyXpResult(7, XpGained: 30m, NewLevel: 1, NewXp: 5m, [], [])));
            acc.Add(Battle(new ProficiencyXpResult(7, XpGained: 20m, NewLevel: 2, NewXp: 3m, [], [])));

            var gain = Assert.Single(acc.Build().Results);
            Assert.Equal(7, gain.ProficiencyId);
            Assert.Equal(50m, gain.XpGained);
            // Level/residual reflect the last battle that touched the proficiency, not a sum.
            Assert.Equal(2, gain.NewLevel);
            Assert.Equal(3m, gain.NewXp);
        }

        [Fact]
        public void Add_UnionsMilestonesAndGrantedSkills_DedupedInFirstSeenOrder()
        {
            var acc = new ProficiencyGainAccumulator();
            acc.Add(Battle(new ProficiencyXpResult(1, 10m, 3, 0m, [3], [100])));
            // A repeat of milestone 3 / skill 100 (idempotent grant) plus new ones must not duplicate.
            acc.Add(Battle(new ProficiencyXpResult(1, 10m, 5, 0m, [3, 5], [100, 200])));

            var gain = Assert.Single(acc.Build().Results);
            Assert.Equal([3, 5], gain.MilestonesCrossed);
            Assert.Equal([100, 200], gain.GrantedSkillIds);
        }

        [Fact]
        public void Add_KeepsProficienciesSeparate_InFirstSeenOrder()
        {
            var acc = new ProficiencyGainAccumulator();
            acc.Add(Battle(
                new ProficiencyXpResult(5, 10m, 1, 0m, [], []),
                new ProficiencyXpResult(2, 4m, 1, 0m, [], [])));
            acc.Add(Battle(new ProficiencyXpResult(2, 6m, 2, 0m, [], [])));

            var results = acc.Build().Results;
            Assert.Equal([5, 2], results.Select(r => r.ProficiencyId));
            Assert.Equal(10m, results[0].XpGained);
            Assert.Equal(10m, results[1].XpGained);
        }

        [Fact]
        public void Add_DedupesOpenedNodes_ByProficiencyInFirstSeenOrder()
        {
            var acc = new ProficiencyGainAccumulator();
            acc.Add(new ProficiencyAccrualResult([], [new ProficiencyOpened(8, SeedSkillId: 80)]));
            acc.Add(new ProficiencyAccrualResult([], [
                new ProficiencyOpened(8, SeedSkillId: 80),
                new ProficiencyOpened(9, SeedSkillId: null)]));

            var opened = acc.Build().Opened;
            Assert.Equal([8, 9], opened.Select(o => o.ProficiencyId));
            Assert.Equal(80, opened[0].SeedSkillId);
            Assert.Null(opened[1].SeedSkillId);
        }

        [Fact]
        public void Add_EmptyAccrualsContributeNothing()
        {
            var acc = new ProficiencyGainAccumulator();
            acc.Add(ProficiencyAccrualResult.Empty);
            acc.Add(Battle(new ProficiencyXpResult(1, 5m, 1, 0m, [], [])));
            acc.Add(ProficiencyAccrualResult.Empty);

            var gain = Assert.Single(acc.Build().Results);
            Assert.Equal(5m, gain.XpGained);
        }
    }
}
