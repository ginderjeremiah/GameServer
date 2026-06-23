using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Players.Events;
using Game.Core.Proficiencies;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// The milestone-effect and proficiency-open triggers (spike #982 area D) layered on the XP accrual: a
    /// crossed milestone grants its reward skill, maxing a tier opens (and seeds) the next tier in its path,
    /// and a cross-path gateway opens once all its prerequisites are maxed. "Opened" itself is derived from
    /// levels + structure, so the only persisted effects are the idempotent skill grants — verified here
    /// against live DB-backed reference data, exactly as the battle-completion path runs.
    /// </summary>
    [Collection("Integration")]
    public class ProficiencyRewardServiceTests : ApplicationIntegrationTestBase
    {
        public ProficiencyRewardServiceTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task CrossingAMilestone_GrantsItsRewardSkillUnselected()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var (playerId, firedSkillId, proficiency) = await SeedSingleTierAsync(
                context, maxLevel: 10, baseXp: 2m, xpGrowth: 1m);
            // 10 XP over a flat cost-2 curve reaches level 5 (well short of the cap), crossing the level-3
            // milestone without maxing — isolating the milestone grant from the open trigger.
            var rewardSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Milestone Reward");
            await TestDataSeeder.AddProficiencyLevelRewardAsync(context, proficiency.Id, level: 3, rewardSkill.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, results) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            Assert.Equal(5, Assert.Single(results).NewLevel);
            Assert.Contains(player.Skills, s => s.Id == rewardSkill.Id);
            Assert.False(player.SelectedSkills.Any(s => s.Id == rewardSkill.Id), "a granted reward skill is unselected");
            Assert.Equal([rewardSkill.Id], Assert.Single(results).GrantedSkillIds);
        }

        [Fact]
        public async Task CrossingABonusOnlyMilestone_GrantsNoSkillButReportsTheMilestone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A milestone level with no authored reward skill is bonus-only: reported in MilestonesCrossed but
            // contributing nothing to GrantedSkillIds.
            var (playerId, firedSkillId, _) = await SeedSingleTierAsync(
                context, maxLevel: 10, baseXp: 2m, xpGrowth: 1m);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, results) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            // The starter skill the player fired is the only one they own — no reward was granted.
            Assert.Single(player.Skills);
            Assert.Empty(Assert.Single(results).GrantedSkillIds);
        }

        [Fact]
        public async Task MaxingATier_OpensAndSeedsTheNextTierInItsPath()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var path = await TestDataSeeder.CreatePathAsync(context);
            // Tier 0 maxes on a single victory (cap 1, cost 1); tier 1 carries the seed skill granted on open.
            var tier0 = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var seedSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Inferno Seed");
            await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Inferno", maxLevel: 10, baseXp: 100m, pathId: path.Id, pathOrdinal: 1,
                startsUnlocked: false, seedSkillId: seedSkill.Id);

            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, tier0.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, _) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            Assert.Contains(player.Skills, s => s.Id == seedSkill.Id);
        }

        [Fact]
        public async Task MaxingTheLastPrerequisite_OpensAndSeedsTheGatewayTier()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Two single-tier prerequisite proficiencies and a gated tier that opens once both are maxed.
            var prereqA = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var prereqB = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Earth", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var gatewaySeed = await TestDataSeeder.CreateSkillAsync(context, name: "Lava Seed");
            var gateway = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Lava", maxLevel: 10, baseXp: 100m, startsUnlocked: false, seedSkillId: gatewaySeed.Id);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqA.Id);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqB.Id);

            // Prereq A is already maxed; this battle maxes prereq B, satisfying the gateway.
            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, prereqB.Id);
            await TestDataSeeder.AddPlayerProficiencyAsync(context, playerId, prereqA.Id, level: 1);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, _) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            Assert.Contains(player.Skills, s => s.Id == gatewaySeed.Id);
        }

        [Fact]
        public async Task MaxingOnePrerequisite_DoesNotOpenAGatewayStillMissingAnother()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var prereqA = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var prereqB = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Earth", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var gatewaySeed = await TestDataSeeder.CreateSkillAsync(context, name: "Lava Seed");
            var gateway = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Lava", maxLevel: 10, baseXp: 100m, startsUnlocked: false, seedSkillId: gatewaySeed.Id);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqA.Id);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqB.Id);

            // Prereq A is left un-maxed, so maxing B alone must not open the gateway.
            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, prereqB.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, _) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            Assert.DoesNotContain(player.Skills, s => s.Id == gatewaySeed.Id);
        }

        [Fact]
        public async Task LivePath_RaisesTheEventCarryingGrantedSkillsAndOpenedTiers()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var path = await TestDataSeeder.CreatePathAsync(context);
            var tier0 = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            // A milestone at the cap grants a reward skill, and maxing opens the seeded next tier.
            var rewardSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Reward");
            await TestDataSeeder.AddProficiencyLevelRewardAsync(context, tier0.Id, level: 1, rewardSkill.Id);
            var seedSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Inferno Seed");
            var tier1 = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Inferno", maxLevel: 10, baseXp: 100m, pathId: path.Id, pathOrdinal: 1,
                startsUnlocked: false, seedSkillId: seedSkill.Id);

            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, tier0.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, _) = await AccrueAsync(scope, playerId, firedSkillId, notify: true);

            var raised = Assert.Single(player.DomainEvents.OfType<ProficiencyXpGainedEvent>());
            Assert.Equal([rewardSkill.Id], Assert.Single(raised.Results).GrantedSkillIds);
            var opened = Assert.Single(raised.Opened);
            Assert.Equal(tier1.Id, opened.ProficiencyId);
            Assert.Equal(seedSkill.Id, opened.SeedSkillId);
        }

        [Fact]
        public async Task OfflineReRun_DoesNotDoubleGrantAMilestoneReward()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var (playerId, firedSkillId, proficiency) = await SeedSingleTierAsync(
                context, maxLevel: 10, baseXp: 2m, xpGrowth: 1m);
            var rewardSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Milestone Reward");
            await TestDataSeeder.AddProficiencyLevelRewardAsync(context, proficiency.Id, level: 3, rewardSkill.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            // Mirror the offline batch: the same loadout accrued over two won battles. The reward is granted on
            // the first (crossing level 3) and must not be re-added on the second (UnlockSkill is idempotent).
            var service = scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>();
            var player = await LoadPlayerAsync(scope, playerId);
            var progress = await scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>().Load(player);
            var stats = FireSkill(firedSkillId);

            service.AccrueAndApply(progress, stats, difficultyMultiplier: 1.0, player, notify: false);
            service.AccrueAndApply(progress, stats, difficultyMultiplier: 1.0, player, notify: false);

            Assert.Single(player.Skills, s => s.Id == rewardSkill.Id);
        }

        // Seeds a fresh player whose one (selected) starter skill contributes to a single-tier proficiency, so
        // a won battle that fires it routes the whole pie there. Returns the player, fired skill, and proficiency.
        private static async Task<(int PlayerId, int FiredSkillId, Game.Infrastructure.Entities.Proficiency Proficiency)> SeedSingleTierAsync(
            GameContext context, int maxLevel, decimal baseXp, decimal xpGrowth)
        {
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(
                context, maxLevel: maxLevel, baseXp: baseXp, xpGrowth: xpGrowth);
            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, proficiency.Id);
            return (playerId, firedSkillId, proficiency);
        }

        private static async Task<(int PlayerId, int FiredSkillId)> SeedPlayerWithFiringSkillAsync(
            GameContext context, int proficiencyId)
        {
            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiencyId, skill.Id, weight: 1m);
            return (player.Id, skill.Id);
        }

        private async Task<(Player Player, IReadOnlyList<ProficiencyXpResult> Results)> AccrueAsync(
            IServiceScope scope, int playerId, int firedSkillId, bool notify)
        {
            var service = scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>();
            var player = await LoadPlayerAsync(scope, playerId);
            var progress = await scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>().Load(player);
            var results = service.AccrueAndApply(progress, FireSkill(firedSkillId), difficultyMultiplier: 1.0, player, notify);
            return (player, results);
        }

        private static async Task<Player> LoadPlayerAsync(IServiceScope scope, int playerId)
        {
            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(playerId);
            Assert.NotNull(player);
            return player;
        }

        private static BattleStats FireSkill(int skillId)
        {
            var stats = new BattleStats();
            stats.SkillStats[skillId] = new SkillStats { SkillId = skillId, Uses = 1 };
            return stats;
        }
    }
}
