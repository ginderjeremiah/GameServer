using Game.Abstractions.DataAccess;
using Game.Application.Events;
using Game.Application.Services;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Players;
using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Events
{
    /// <summary>
    /// Handler-level coverage for <see cref="BattleStatisticsEventHandler"/>'s challenge-reward
    /// orchestration: when a battle completes a challenge, the handler resolves the reward id via the
    /// relevant provider (<see cref="IItems"/>; mods are unlocked by id directly) and applies the matching
    /// unlock to the player aggregate. Each reward kind is exercised the same way, alongside the additive
    /// multi-reward case and the null-reward edge (#292). Challenges no longer grant skills (spike #982).
    /// </summary>
    [Collection("Integration")]
    public class BattleStatisticsEventHandlerTests : ApplicationIntegrationTestBase
    {
        public BattleStatisticsEventHandlerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task CompletingChallengeWithItemReward_UnlocksItemOnPlayer()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: true, modReward: false);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            Assert.Contains(player.Inventory.UnlockedItems, u => u.ItemId == setup.ItemId);
            // The mod reward id was null, so it is not unlocked.
            Assert.Empty(player.Inventory.UnlockedMods);
        }

        [Fact]
        public async Task CompletingChallengeWithModReward_UnlocksModOnPlayer()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: false, modReward: true);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            Assert.Contains(setup.ModId, player.Inventory.UnlockedMods);
            // The item reward id was null, so it is not unlocked.
            Assert.Empty(player.Inventory.UnlockedItems);
        }

        [Fact]
        public async Task CompletingChallengeWithMultipleRewards_AppliesAllAdditively()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: true, modReward: true);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            // A single challenge carrying every reward kind unlocks all of them.
            Assert.Contains(player.Inventory.UnlockedItems, u => u.ItemId == setup.ItemId);
            Assert.Contains(setup.ModId, player.Inventory.UnlockedMods);
        }

        [Fact]
        public async Task CompletingChallenge_RaisesChallengeCompletedEventWithRewardIds()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: true, modReward: true);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            // The completion is announced for the client push, carrying every reward id it unlocked so the
            // client can make them usable immediately.
            var evt = Assert.Single(player.DomainEvents.OfType<ChallengeCompletedEvent>());
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(setup.ItemId, evt.RewardItemId);
            Assert.Equal(setup.ModId, evt.RewardItemModId);
        }

        [Fact]
        public async Task CompletingChallengeWithNoRewards_UnlocksNothing()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: false, modReward: false);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            // The challenge genuinely completed (so the unlock loop ran) — confirm via the progress cache the
            // handler wrote, rather than relying on the absence of unlocks (which would also hold if it never
            // completed). The write-behind save makes the cache the source of truth, so the DB is not read.
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            Assert.NotEmpty(await progressRepo.GetCompletedChallengeIds(setup.PlayerId));

            // A challenge with all-null reward ids is a clean no-op for unlocks.
            Assert.Empty(player.Inventory.UnlockedItems);
            Assert.Empty(player.Inventory.UnlockedMods);
        }

        [Fact]
        public async Task CompletingStatisticIndependentChallenge_CompletesThroughTheRelevanceIndex()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            // The seeder's default player level (5) meets the LevelReached goal below.
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var challenge = await TestDataSeeder.CreateChallengeAsync(
                context, challengeTypeId: EChallengeType.LevelReached, progressGoal: 5m);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var handler = new BattleStatisticsEventHandler(
                scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>(),
                scope.ServiceProvider.GetRequiredService<ChallengeRewardService>(),
                scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>());

            // A LevelReached challenge tracks no recorded statistic, so it is only reached via the index's
            // statistic-independent set — which any completed battle evaluates, even this empty-stats victory.
            var battleEvent = new BattleCompletedEvent(
                loadedPlayer, loadedEnemy, Victory: true, PlayerDied: false, TotalMs: 5000,
                Stats: new BattleStats(), IsBossBattle: false, ZoneId: loadedPlayer.CurrentZoneId);
            await handler.HandleAsync(battleEvent, CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            Assert.Contains(challenge.Id, await progressRepo.GetCompletedChallengeIds(player.Id));
        }

        [Fact]
        public async Task RetiredChallenge_IsReachedByTheIndexButNotCompleted()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            // A retired challenge whose goal a single kill would satisfy. The victory still moves the
            // EnemiesKilled statistic, so the relevance index reaches the challenge — but retirement takes it
            // out of circulation, so the player (who never completed it) can no longer complete it.
            var retired = await TestDataSeeder.CreateChallengeAsync(
                context, challengeTypeId: EChallengeType.EnemiesKilled, progressGoal: 1m, retiredAt: DateTime.UtcNow);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var handler = new BattleStatisticsEventHandler(
                scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>(),
                scope.ServiceProvider.GetRequiredService<ChallengeRewardService>(),
                scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>());

            var battleEvent = new BattleCompletedEvent(
                loadedPlayer, loadedEnemy, Victory: true, PlayerDied: false, TotalMs: 5000,
                Stats: new BattleStats(), IsBossBattle: false, ZoneId: loadedPlayer.CurrentZoneId);
            await handler.HandleAsync(battleEvent, CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            Assert.DoesNotContain(retired.Id, await progressRepo.GetCompletedChallengeIds(player.Id));
        }

        [Fact]
        public async Task Victory_WithContributingSkillFired_AccruesProficiencyXpAndAnnouncesIt()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, name: "Fire");
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, skill.Id, weight: 1m);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var handler = MakeHandler(scope);

            // The skill fired once in the won battle, so its proficiency is represented and (being the only one)
            // takes the whole pie; below the first threshold it accrues without leveling.
            var stats = new BattleStats();
            stats.SkillStats[skill.Id] = new SkillStats { Uses = 1 };
            await handler.HandleAsync(VictoryEvent(loadedPlayer, loadedEnemy, stats, difficultyMultiplier: 1.0), CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var stored = Assert.Single(await progressRepo.GetProficiencies(player.Id));
            Assert.Equal(proficiency.Id, stored.ProficiencyId);
            Assert.Equal(0, stored.Level);
            Assert.Equal((decimal)ServerGameConstants.ProficiencyXpPerVictory, stored.Xp);

            // The accrual is announced for the live client push, carrying the per-proficiency result.
            var evt = Assert.Single(loadedPlayer.DomainEvents.OfType<ProficiencyXpGainedEvent>());
            Assert.Equal(player.Id, evt.PlayerId);
            var result = Assert.Single(evt.Results);
            Assert.Equal(proficiency.Id, result.ProficiencyId);
            Assert.Equal((decimal)ServerGameConstants.ProficiencyXpPerVictory, result.XpGained);
            Assert.Equal(0, result.NewLevel);
        }

        [Fact]
        public async Task Victory_CoastingOnAStaleSkill_AccruesTheDiscountedPie_NotTheFullPie()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);

            // A two-tier Fire path (falloff 0.3). The only contributing skill is homed at tier 0, but the
            // player has already maxed tier 0, so the path's frontier is tier 1 — the stale skill supplements
            // it one tier behind, at the 0.3 discount, and the un-earned 0.7 of the pie evaporates (the
            // absolute slowdown, end to end). The maxed tier 0 banks nothing more.
            const decimal falloffBase = 0.3m;
            const int maxLevel = 10;
            var path = await TestDataSeeder.CreatePathAsync(context, name: "Fire", falloffBase: falloffBase);
            var tierZero = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire Magic", maxLevel: maxLevel, pathId: path.Id, pathOrdinal: 0);
            var tierOne = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Inferno Magic", maxLevel: maxLevel, pathId: path.Id, pathOrdinal: 1);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, tierZero.Id, skill.Id, weight: 1m);
            await TestDataSeeder.AddPlayerProficiencyAsync(context, player.Id, tierZero.Id, level: maxLevel);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var stats = new BattleStats();
            stats.SkillStats[skill.Id] = new SkillStats { Uses = 1 };
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(loadedPlayer, loadedEnemy, stats, difficultyMultiplier: 1.0), CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var stored = await progressRepo.GetProficiencies(player.Id);

            // The frontier tier banks only the discounted pie (0.3 × pie), not the full pie a relative split
            // would have minted for a solo path.
            var frontier = Assert.Single(stored, p => p.ProficiencyId == tierOne.Id);
            Assert.Equal(0, frontier.Level);
            Assert.Equal((decimal)(ServerGameConstants.ProficiencyXpPerVictory * (double)falloffBase), frontier.Xp);

            var maxed = Assert.Single(stored, p => p.ProficiencyId == tierZero.Id);
            Assert.Equal(maxLevel, maxed.Level);
            Assert.Equal(0m, maxed.Xp);
        }

        [Fact]
        public async Task Victory_RarerFiredSkill_PullsALargerShareOfThePie(/* #1123 */)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Two single-tier paths, each fed on-tier by one skill, both fired in the won battle. The skills
            // differ only in rarity — Common (tier weight 1) vs Rare (tier weight 1.5² = 2.25) — so the pie
            // splits by their attention ratio: the rare skill's path claims 2.25 / 3.25 of the pie, the common
            // path 1 / 3.25. Tier weight is the only thing differing, so this isolates the #1123 curve.
            var commonSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt", rarity: ERarity.Common);
            var rareSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Stoneskin", rarity: ERarity.Rare);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, commonSkill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, rareSkill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);

            var firePath = await TestDataSeeder.CreatePathAsync(context, name: "Fire");
            var earthPath = await TestDataSeeder.CreatePathAsync(context, name: "Earth");
            var fireTier = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire Magic", pathId: firePath.Id, pathOrdinal: 0);
            var earthTier = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Earth Magic", pathId: earthPath.Id, pathOrdinal: 0);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, fireTier.Id, commonSkill.Id, weight: 1m);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, earthTier.Id, rareSkill.Id, weight: 1m);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var stats = new BattleStats();
            stats.SkillStats[commonSkill.Id] = new SkillStats { Uses = 1 };
            stats.SkillStats[rareSkill.Id] = new SkillStats { Uses = 1 };
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(loadedPlayer, loadedEnemy, stats, difficultyMultiplier: 1.0), CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var stored = await progressRepo.GetProficiencies(player.Id);

            const double commonWeight = 1.0;
            const double rareWeight = 1.5 * 1.5;
            const double totalWeight = commonWeight + rareWeight;
            var pie = ServerGameConstants.ProficiencyXpPerVictory;

            var fire = Assert.Single(stored, p => p.ProficiencyId == fireTier.Id);
            Assert.Equal((decimal)(pie * commonWeight / totalWeight), fire.Xp, precision: 3);
            var earth = Assert.Single(stored, p => p.ProficiencyId == earthTier.Id);
            Assert.Equal((decimal)(pie * rareWeight / totalWeight), earth.Xp, precision: 3);
            // The rarer skill's path banks strictly more of the same pie.
            Assert.True(earth.Xp > fire.Xp);
        }

        [Fact]
        public async Task Victory_SkillThatDidNotFire_AccruesNoProficiencyXp()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, name: "Fire");
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, skill.Id, weight: 1m);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            // A victory where no skill fired (empty skill stats): nothing is represented, so no proficiency is
            // trained — representation, not the victory itself, is what accrues XP.
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(loadedPlayer, loadedEnemy, new BattleStats(), difficultyMultiplier: 1.0), CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            Assert.Empty(await progressRepo.GetProficiencies(player.Id));
            Assert.Empty(loadedPlayer.DomainEvents.OfType<ProficiencyXpGainedEvent>());
        }

        [Fact]
        public async Task ProficiencyXpAccrual_OfflineMatchesLive_ForTheSameBattle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Two identically-set-up players: one accrues through the live handler, the other through the
            // shared accrual the offline batch calls — the same (stats, difficulty multiplier) on both.
            var liveUser = await TestDataSeeder.CreateUserAsync(context, username: "live-player");
            var livePlayer = await TestDataSeeder.CreatePlayerAsync(context, liveUser.Id);
            var offlineUser = await TestDataSeeder.CreateUserAsync(context, username: "offline-player");
            var offlinePlayer = await TestDataSeeder.CreatePlayerAsync(context, offlineUser.Id);

            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, livePlayer.Id, skill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, offlinePlayer.Id, skill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, name: "Fire");
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, skill.Id, weight: 1m);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var stats = new BattleStats();
            stats.SkillStats[skill.Id] = new SkillStats { Uses = 1 };
            const double multiplier = 1.5;

            var liveLoaded = await playerRepo.GetPlayer(livePlayer.Id);
            Assert.NotNull(liveLoaded);
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(liveLoaded, loadedEnemy, stats, multiplier), CancellationToken);

            var offlineLoaded = await playerRepo.GetPlayer(offlinePlayer.Id);
            Assert.NotNull(offlineLoaded);
            var offlineProgress = await progressRepo.Load(offlineLoaded);
            scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>()
                .AccrueAndApply(offlineProgress, stats, multiplier, offlineLoaded, notify: false);
            await progressRepo.Save(offlineProgress);

            var live = Assert.Single(await progressRepo.GetProficiencies(livePlayer.Id));
            var offline = Assert.Single(await progressRepo.GetProficiencies(offlinePlayer.Id));
            Assert.Equal(live.Level, offline.Level);
            Assert.Equal(live.Xp, offline.Xp);
            // Sanity: the shared accrual actually produced XP (pie × 1.5), so the equality isn't vacuous.
            Assert.Equal((decimal)(ServerGameConstants.ProficiencyXpPerVictory * multiplier), live.Xp);
        }

        private BattleStatisticsEventHandler MakeHandler(IServiceScope scope) => new(
            scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>(),
            scope.ServiceProvider.GetRequiredService<ChallengeRewardService>(),
            scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>());

        private static BattleCompletedEvent VictoryEvent(Player player, Game.Core.Enemies.Enemy enemy, BattleStats stats, double difficultyMultiplier) =>
            new(player, enemy, Victory: true, PlayerDied: false, TotalMs: 5000,
                Stats: stats, IsBossBattle: false, ZoneId: player.CurrentZoneId, DifficultyMultiplier: difficultyMultiplier);

        /// <summary>
        /// Seeds a fresh player (with one starter, equipped skill), an enemy, and one candidate of each
        /// reward kind, plus an <see cref="EChallengeType.EnemiesKilled"/> challenge whose goal of 1 a single
        /// victory satisfies. Only the requested reward kinds are attached to the challenge, so the
        /// unattached candidates must remain locked. Refreshes the static reference caches so the handler's
        /// providers resolve exactly what was seeded.
        /// </summary>
        private async Task<Setup> SeedScenarioAsync(
            IServiceScope scope, bool itemReward, bool modReward)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // A distinct, already-equipped starter skill so the player has a non-empty selected loadout.
            var starterSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Starter");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, starterSkill.Id);

            var enemy = await TestDataSeeder.CreateEnemyAsync(context);

            var item = await TestDataSeeder.CreateItemAsync(context);
            var mod = await TestDataSeeder.CreateItemModAsync(context);

            await TestDataSeeder.CreateChallengeAsync(
                context,
                challengeTypeId: EChallengeType.EnemiesKilled,
                progressGoal: 1m,
                rewardItemId: itemReward ? item.Id : null,
                rewardItemModId: modReward ? mod.Id : null);

            // The caches no longer lazily refill, so reload them to pick up the rows just seeded before the
            // handler reads through its providers.
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            return new Setup(player.Id, enemy.Id, item.Id, mod.Id);
        }

        /// <summary>
        /// Loads the player, builds a victorious <see cref="BattleCompletedEvent"/> that meets the seeded
        /// challenge's goal, and runs <see cref="BattleStatisticsEventHandler"/> over it. The handler is
        /// constructed with its real injected dependencies — exactly how the domain-event dispatcher wires
        /// it — so the orchestration is exercised end to end against live providers.
        /// </summary>
        private async Task<Player> CompleteChallengeVictoryAsync(IServiceScope scope, Setup setup)
        {
            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(setup.PlayerId);
            Assert.NotNull(player);

            var enemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(setup.EnemyId, level: 1);
            Assert.NotNull(enemy);

            var handler = new BattleStatisticsEventHandler(
                scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>(),
                scope.ServiceProvider.GetRequiredService<ChallengeRewardService>(),
                scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>());

            var battleEvent = new BattleCompletedEvent(
                player, enemy, Victory: true, PlayerDied: false, TotalMs: 5000,
                Stats: new BattleStats(), IsBossBattle: false, ZoneId: player.CurrentZoneId);

            await handler.HandleAsync(battleEvent, CancellationToken);
            return player;
        }

        private sealed record Setup(int PlayerId, int EnemyId, int ItemId, int ModId);
    }
}
