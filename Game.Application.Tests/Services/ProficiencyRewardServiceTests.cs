using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core;
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
    /// crossed milestone grants its reward skill, maxing a tier opens the next tier in its path, and a
    /// cross-path gateway opens once all its prerequisites are maxed. Opening is notification-only — no skill
    /// is granted on open (the freshly-revealed tier's native skill is re-homed onto the predecessor tier's
    /// max-level milestone reward — skill synthesis, spike #1125). "Opened" itself is derived from levels +
    /// structure, so the only persisted effects are the idempotent milestone skill grants — verified here
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

            var (player, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            Assert.Equal(5, Assert.Single(accrual.Results).NewLevel);
            Assert.Contains(player.Skills, s => s.Id == rewardSkill.Id);
            Assert.False(player.SelectedSkills.Any(s => s.Id == rewardSkill.Id), "a granted reward skill is unselected");
            Assert.Equal([rewardSkill.Id], Assert.Single(accrual.Results).GrantedSkillIds);
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

            var (player, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            // The starter skill the player fired is the only one they own — no reward was granted.
            Assert.Single(player.Skills);
            Assert.Empty(Assert.Single(accrual.Results).GrantedSkillIds);
        }

        [Fact]
        public async Task MaxingATier_OpensTheNextTierInItsPath_WithoutGrantingASkill()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var path = await TestDataSeeder.CreatePathAsync(context);
            // Tier 0 maxes on a single victory (cap 1, cost 1); maxing it reveals tier 1 — notification-only.
            var tier0 = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var tier1 = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Inferno", maxLevel: 10, baseXp: 100m, pathId: path.Id, pathOrdinal: 1);

            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, tier0.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            // Tier 1 is reported opened for the client push, but opening grants no skill — the player still owns
            // only the starter skill they fired.
            Assert.Contains(accrual.Opened, o => o.ProficiencyId == tier1.Id);
            Assert.Single(player.Skills);
        }

        [Fact]
        public async Task FreshKitSkill_OpensItsPathOnFirstContribution_WithoutStartsUnlocked()
        {
            // Regression for the StartsUnlocked retirement (spike #1126): roots are no longer authored as
            // "universally open" — they emerge from the class kit. A brand-new player (every proficiency at
            // level 0) whose starter skill contributes to a path's root tier accrues XP to that tier on the
            // first won battle, levelling it from 0 — i.e. the kit opens the path with no StartsUnlocked seeding.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var path = await TestDataSeeder.CreatePathAsync(context);
            var root = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire", maxLevel: 10, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, root.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (_, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            // The root tier accrued from level 0 on the first contribution — the path opened without a flag.
            Assert.True(Assert.Single(accrual.Results).NewLevel >= 1);
        }

        [Fact]
        public async Task MaxingTheLastPrerequisite_OpensTheGatewayTier_WithoutGrantingASkill()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Two single-tier prerequisite proficiencies and a gated tier that opens once both are maxed.
            var prereqA = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var prereqB = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Earth", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var gateway = await TestDataSeeder.CreateProficiencyAsync(context, name: "Lava", maxLevel: 10, baseXp: 100m);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqA.Id);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqB.Id);

            // Prereq A is already maxed; this battle maxes prereq B, satisfying the gateway.
            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, prereqB.Id);
            await TestDataSeeder.AddPlayerProficiencyAsync(context, playerId, prereqA.Id, level: 1);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            // The gateway opens (reported for the client push) but grants no skill.
            Assert.Contains(accrual.Opened, o => o.ProficiencyId == gateway.Id);
            Assert.Single(player.Skills);
        }

        [Fact]
        public async Task MaxingOnePrerequisite_DoesNotOpenAGatewayStillMissingAnother()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var prereqA = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var prereqB = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Earth", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var gateway = await TestDataSeeder.CreateProficiencyAsync(context, name: "Lava", maxLevel: 10, baseXp: 100m);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqA.Id);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqB.Id);

            // Prereq A is left un-maxed, so maxing B alone must not open the gateway.
            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, prereqB.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (_, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            Assert.DoesNotContain(accrual.Opened, o => o.ProficiencyId == gateway.Id);
        }

        [Fact]
        public async Task MaxingThePrerequisitesOfARetiredGateway_DoesNotOpenIt()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The gateway tier lives on a retired (frozen) path; its prerequisites are on live paths. Maxing
            // both prerequisites here would open the gateway if it were live, but a retired track must never be
            // opened via the open logic — so the gateway is not reported opened.
            var prereqA = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var prereqB = await TestDataSeeder.CreateProficiencyAsync(context, name: "Adv Earth", maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var retiredPath = await TestDataSeeder.CreatePathAsync(context, name: "Retired Lava", retiredAt: DateTime.UtcNow);
            var gateway = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Lava", maxLevel: 10, baseXp: 100m, pathId: retiredPath.Id, pathOrdinal: 0);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqA.Id);
            await TestDataSeeder.AddProficiencyPrerequisiteAsync(context, gateway.Id, prereqB.Id);

            // Prereq A is already maxed; this battle maxes prereq B, which would otherwise satisfy the gateway.
            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, prereqB.Id);
            await TestDataSeeder.AddPlayerProficiencyAsync(context, playerId, prereqA.Id, level: 1);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (_, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            // Prereq B genuinely maxed (so the gateway's prerequisites are all satisfied) — what's suppressed is
            // the gateway open, not a dead accrual.
            Assert.Equal(1, Assert.Single(accrual.Results).NewLevel);
            Assert.DoesNotContain(accrual.Opened, o => o.ProficiencyId == gateway.Id);
        }

        [Fact]
        public async Task LivePath_RaisesTheEventCarryingGrantedSkillsAndOpenedTiers()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var path = await TestDataSeeder.CreatePathAsync(context);
            var tier0 = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire", maxLevel: 1, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            // A milestone at the cap grants a reward skill (the within-path native, re-homed from the old seed),
            // and maxing the tier opens the next tier — notification-only.
            var rewardSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Reward");
            await TestDataSeeder.AddProficiencyLevelRewardAsync(context, tier0.Id, level: 1, rewardSkill.Id);
            var tier1 = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Inferno", maxLevel: 10, baseXp: 100m, pathId: path.Id, pathOrdinal: 1);

            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, tier0.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, _) = await AccrueAsync(scope, playerId, firedSkillId, notify: true);

            var raised = Assert.Single(player.DomainEvents.OfType<ProficiencyXpGainedEvent>());
            Assert.Equal([rewardSkill.Id], Assert.Single(raised.Results).GrantedSkillIds);
            var opened = Assert.Single(raised.Opened);
            Assert.Equal(tier1.Id, opened.ProficiencyId);
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

            service.AccrueAndApply(progress, stats, totalAttributes: FiredDamage, player, notify: false);
            service.AccrueAndApply(progress, stats, totalAttributes: FiredDamage, player, notify: false);

            Assert.Single(player.Skills, s => s.Id == rewardSkill.Id);
        }

        [Fact]
        public async Task RetiredPath_FreezesTheTrack_AccruesNothingAndGrantsNoSkill()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A path taken out of circulation: a fired skill contributing to it routes no XP, so the tier never
            // levels and its cap milestone reward is never granted — the whole track is frozen. The curve would
            // otherwise max the tier (cap 1, cost 1) and grant the reward on a single victory.
            var path = await TestDataSeeder.CreatePathAsync(context, retiredAt: DateTime.UtcNow);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(
                context, maxLevel: 1, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var rewardSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Milestone Reward");
            await TestDataSeeder.AddProficiencyLevelRewardAsync(context, proficiency.Id, level: 1, rewardSkill.Id);

            var (playerId, firedSkillId) = await SeedPlayerWithFiringSkillAsync(context, proficiency.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (player, accrual) = await AccrueAsync(scope, playerId, firedSkillId, notify: false);

            Assert.Empty(accrual.Results);
            Assert.DoesNotContain(player.Skills, s => s.Id == rewardSkill.Id);
            // The player owns only the starter skill they fired — no proficiency reward was granted.
            Assert.Single(player.Skills);
        }

        [Fact]
        public async Task CritDamage_TrainsThePrecisionPath()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A path keyed on the Crit event trains from the battle's crit damage — no skill contribution and no
            // damage-type routing involved (Precision is a single global, type-neutral track; spike #1318).
            var path = await TestDataSeeder.CreatePathAsync(context, name: "Precision", activityKey: EActivityKey.Crit);
            var tier = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Precision", maxLevel: 10, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (_, accrual) = await AccrueStatsAsync(scope, playerId, new BattleStats { CriticalDamageDealt = FiredDamage });

            var result = Assert.Single(accrual.Results);
            Assert.Equal(tier.Id, result.ProficiencyId);
            Assert.True(result.NewLevel >= 1);
        }

        [Fact]
        public async Task DodgedDamage_TrainsTheEvasionPath()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The Evasion track trains from dodged damage (the avoided post-Defense hit) — incoming-book event,
            // type-neutral like Precision.
            var path = await TestDataSeeder.CreatePathAsync(context, name: "Evasion", activityKey: EActivityKey.Dodge);
            var tier = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Evasion", maxLevel: 10, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (_, accrual) = await AccrueStatsAsync(scope, playerId, new BattleStats { DamageDodged = FiredDamage });

            var result = Assert.Single(accrual.Results);
            Assert.Equal(tier.Id, result.ProficiencyId);
            Assert.True(result.NewLevel >= 1);
        }

        [Fact]
        public async Task HealingDone_TrainsTheRestorationPath()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Restoration trains from healing done (PlayerDamageHealed) — an output-book event.
            var path = await TestDataSeeder.CreatePathAsync(context, name: "Restoration", activityKey: EActivityKey.Heal);
            var tier = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Restoration", maxLevel: 10, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var (_, accrual) = await AccrueStatsAsync(scope, playerId, new BattleStats { PlayerDamageHealed = FiredDamage });

            var result = Assert.Single(accrual.Results);
            Assert.Equal(tier.Id, result.ProficiencyId);
            Assert.True(result.NewLevel >= 1);
        }

        [Fact]
        public async Task ReflectedDamage_IsNotYetWired_SoARetributionPathTrainsNothing()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Retribution waits on the reflection rework (#1330) — no reflected-damage signal is produced yet, so
            // a Reflect-keyed path accrues nothing even from a battle full of every other event activity.
            var path = await TestDataSeeder.CreatePathAsync(context, name: "Retribution", activityKey: EActivityKey.Reflect);
            await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Retribution", maxLevel: 10, baseXp: 1m, xpGrowth: 1m, pathId: path.Id, pathOrdinal: 0);
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var stats = new BattleStats
            {
                CriticalDamageDealt = FiredDamage,
                DamageDodged = FiredDamage,
                PlayerDamageHealed = FiredDamage,
            };
            var (_, accrual) = await AccrueStatsAsync(scope, playerId, stats);

            // Only a Reflect path exists, and Reflect is unwired — nothing trains.
            Assert.Empty(accrual.Results);
        }

        [Fact]
        public async Task OffenseAndEventAxes_TrainInParallel_WithoutDilution()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Spike #1318 decision 4: axes do not share a pie. A battle that both deals fire damage and lands
            // crits trains the Fire offense path and the Precision event path each at activity ÷ power = 1 — with
            // identical curves they reach the same level, proving neither axis dilutes the other.
            var firePath = await TestDataSeeder.CreatePathAsync(context, name: "Fire", activityKey: EActivityKey.Fire);
            var fireTier = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire", maxLevel: 10, baseXp: 1m, xpGrowth: 1m, pathId: firePath.Id, pathOrdinal: 0);
            var critPath = await TestDataSeeder.CreatePathAsync(context, name: "Precision", activityKey: EActivityKey.Crit);
            var critTier = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Precision", maxLevel: 10, baseXp: 1m, xpGrowth: 1m, pathId: critPath.Id, pathOrdinal: 0);

            var fireSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt", damageType: EDamageType.Fire);
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            // Full-power fire damage and full-power crit damage in the same battle.
            var stats = new BattleStats { CriticalDamageDealt = FiredDamage };
            stats.SkillStats[fireSkill.Id] = new SkillStats { Uses = 1, TotalDamage = FiredDamage };
            var (_, accrual) = await AccrueStatsAsync(scope, playerId, stats);

            var fireResult = Assert.Single(accrual.Results, r => r.ProficiencyId == fireTier.Id);
            var critResult = Assert.Single(accrual.Results, r => r.ProficiencyId == critTier.Id);
            Assert.True(fireResult.NewLevel >= 1);
            Assert.Equal(fireResult.NewLevel, critResult.NewLevel);
            Assert.Equal(fireResult.XpGained, critResult.XpGained);
        }

        // The damage a fired skill deals in these tests, equal to the power passed to the accrual — so
        // activity ÷ power = 1 and each won battle claims the full pie (ServerGameConstants.ProficiencyXpPerVictory),
        // keeping the per-level math identical to the assertions below.
        private const double FiredDamage = 100.0;

        // Seeds a fresh player whose one (selected) starter skill trains a single-tier proficiency's path, so a
        // won battle that fires it routes the full pie there. Returns the player, fired skill, and proficiency.
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
            // A Fire-typed skill, so it routes only to the (Fire-keyed) path it is linked to and leaves the
            // other test paths (Physical by default) untrained — the isolation the old contribution join gave.
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt", damageType: EDamageType.Fire);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiencyId, skill.Id);
            return (player.Id, skill.Id);
        }

        private async Task<(Player Player, ProficiencyAccrualResult Accrual)> AccrueAsync(
            IServiceScope scope, int playerId, int firedSkillId, bool notify)
        {
            var service = scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>();
            var player = await LoadPlayerAsync(scope, playerId);
            var progress = await scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>().Load(player);
            var accrual = service.AccrueAndApply(progress, FireSkill(firedSkillId), totalAttributes: FiredDamage, player, notify);
            return (player, accrual);
        }

        // Accrues an arbitrary battle's stats (the event bindings build their own BattleStats rather than firing a
        // skill), normalized by the default power so a full-power activity claims the whole pie.
        private async Task<(Player Player, ProficiencyAccrualResult Accrual)> AccrueStatsAsync(
            IServiceScope scope, int playerId, BattleStats stats, bool notify = false)
        {
            var service = scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>();
            var player = await LoadPlayerAsync(scope, playerId);
            var progress = await scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>().Load(player);
            var accrual = service.AccrueAndApply(progress, stats, totalAttributes: FiredDamage, player, notify);
            return (player, accrual);
        }

        // Seeds a bare player (no skills) — the event bindings train from BattleStats event fields, not from any
        // fired skill, so no loadout is needed.
        private static async Task<int> SeedPlayerAsync(GameContext context)
        {
            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return player.Id;
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
            stats.SkillStats[skillId] = new SkillStats { Uses = 1, TotalDamage = FiredDamage };
            return stats;
        }
    }
}
