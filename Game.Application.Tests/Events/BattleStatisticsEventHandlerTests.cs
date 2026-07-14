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
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, skill.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var handler = MakeHandler(scope);

            // The skill dealt damage equal to the player's power, so its path claims the full pie
            // (clamp(activity ÷ power) = 1); below the first threshold it accrues without leveling.
            const double power = 100.0;
            var stats = new BattleStats { SkillStats = { [skill.Id] = new SkillStats { Uses = 1, TotalDamage = power } } };
            stats.AddTypedDamageDealt(EDamageType.Physical, power); // the offense book the accrual consumes
            await handler.HandleAsync(VictoryEvent(loadedPlayer, loadedEnemy, stats, playerRating: power), CancellationToken);

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
        public async Task Victory_FrontierTier_TrainsAtFullPace_WhenLowerTierMaxed()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);

            // A two-tier path whose tier 0 the player has already maxed, so the frontier is tier 1. The skill's
            // damage routes to the path and trains the frontier at full pace — there is no falloff/staleness
            // discount in the effect-based model (the maxed tier 0 simply banks nothing more).
            const int maxLevel = 10;
            const double power = 100.0;
            var path = await TestDataSeeder.CreatePathAsync(context, name: "Fire");
            var tierZero = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Fire Magic", maxLevel: maxLevel, pathId: path.Id, pathOrdinal: 0);
            var tierOne = await TestDataSeeder.CreateProficiencyAsync(
                context, name: "Inferno Magic", maxLevel: maxLevel, pathId: path.Id, pathOrdinal: 1);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, tierZero.Id, skill.Id);
            await TestDataSeeder.AddPlayerProficiencyAsync(context, player.Id, tierZero.Id, level: maxLevel);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            var stats = new BattleStats { SkillStats = { [skill.Id] = new SkillStats { Uses = 1, TotalDamage = power } } };
            stats.AddTypedDamageDealt(EDamageType.Physical, power); // the offense book the accrual consumes
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(loadedPlayer, loadedEnemy, stats, playerRating: power), CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var stored = await progressRepo.GetProficiencies(player.Id);

            // The frontier tier banks the full pie (activity ÷ power = 1, no discount).
            var frontier = Assert.Single(stored, p => p.ProficiencyId == tierOne.Id);
            Assert.Equal(0, frontier.Level);
            Assert.Equal((decimal)ServerGameConstants.ProficiencyXpPerVictory, frontier.Xp);

            var maxed = Assert.Single(stored, p => p.ProficiencyId == tierZero.Id);
            Assert.Equal(maxLevel, maxed.Level);
            Assert.Equal(0m, maxed.Xp);
        }

        [Fact]
        public async Task Victory_TwoTypedSkills_TrainTheirPathsIndependently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Two single-tier paths trained by two distinct-typed skills. Each path claims pie × clamp(its own
            // damage ÷ power) independently — the claims overlap and need not sum to 1 (no shared pie). Fire
            // deals the full power in damage (claims the full pie); Earth deals half (claims half the pie).
            var fireSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt", damageType: EDamageType.Fire);
            var earthSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Stoneskin", damageType: EDamageType.Earth);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, fireSkill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, earthSkill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);

            var fireTier = await TestDataSeeder.CreateProficiencyAsync(context, name: "Fire Magic");
            var earthTier = await TestDataSeeder.CreateProficiencyAsync(context, name: "Earth Magic");
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, fireTier.Id, fireSkill.Id);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, earthTier.Id, earthSkill.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            const double power = 100.0;
            var stats = new BattleStats
            {
                SkillStats =
                {
                    [fireSkill.Id] = new SkillStats { Uses = 1, TotalDamage = power },
                    [earthSkill.Id] = new SkillStats { Uses = 1, TotalDamage = power / 2 },
                },
            };
            // The typed offense book the accrual consumes: Fire deals the full power, Earth half.
            stats.AddTypedDamageDealt(EDamageType.Fire, power);
            stats.AddTypedDamageDealt(EDamageType.Earth, power / 2);
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(loadedPlayer, loadedEnemy, stats, playerRating: power), CancellationToken);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var stored = await progressRepo.GetProficiencies(player.Id);
            var pie = ServerGameConstants.ProficiencyXpPerVictory;

            var fire = Assert.Single(stored, p => p.ProficiencyId == fireTier.Id);
            Assert.Equal((decimal)pie, fire.Xp, precision: 3);
            var earth = Assert.Single(stored, p => p.ProficiencyId == earthTier.Id);
            Assert.Equal((decimal)(pie * 0.5), earth.Xp, precision: 3);
        }

        [Fact]
        public async Task Victory_NoDamageDealt_AccruesNoProficiencyXp()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, name: "Fire");
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, skill.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var loadedPlayer = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(player.Id);
            Assert.NotNull(loadedPlayer);
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            // A victory with empty battle stats (no skill dealt damage): no activity, so no path is trained —
            // the effect (damage), not the victory itself, is what accrues XP.
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(loadedPlayer, loadedEnemy, new BattleStats(), playerRating: 100.0), CancellationToken);

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
            // shared accrual the offline batch calls — the same (stats, player power) on both.
            var liveUser = await TestDataSeeder.CreateUserAsync(context, username: "live-player");
            var livePlayer = await TestDataSeeder.CreatePlayerAsync(context, liveUser.Id);
            var offlineUser = await TestDataSeeder.CreateUserAsync(context, username: "offline-player");
            var offlinePlayer = await TestDataSeeder.CreatePlayerAsync(context, offlineUser.Id);

            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, livePlayer.Id, skill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, offlinePlayer.Id, skill.Id);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, name: "Fire");
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, skill.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var loadedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(loadedEnemy);

            // The skill dealt 1.5× the player's power, so each path claims pie × clamp(1.5) = pie × 1.5 — the
            // same on both the live and the offline path.
            const double power = 100.0;
            var stats = new BattleStats { SkillStats = { [skill.Id] = new SkillStats { Uses = 1, TotalDamage = power * 1.5 } } };
            stats.AddTypedDamageDealt(EDamageType.Physical, power * 1.5); // the offense book the accrual consumes

            var liveLoaded = await playerRepo.GetPlayer(livePlayer.Id);
            Assert.NotNull(liveLoaded);
            await MakeHandler(scope).HandleAsync(
                VictoryEvent(liveLoaded, loadedEnemy, stats, playerRating: power), CancellationToken);

            var offlineLoaded = await playerRepo.GetPlayer(offlinePlayer.Id);
            Assert.NotNull(offlineLoaded);
            var offlineProgress = await progressRepo.Load(offlineLoaded);
            scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>()
                .AccrueAndApply(offlineProgress, stats, ratingDenominator: power, offlineLoaded, notify: false);
            await progressRepo.Save(offlineProgress);

            var live = Assert.Single(await progressRepo.GetProficiencies(livePlayer.Id));
            var offline = Assert.Single(await progressRepo.GetProficiencies(offlinePlayer.Id));
            Assert.Equal(live.Level, offline.Level);
            Assert.Equal(live.Xp, offline.Xp);
            // Sanity: the shared accrual actually produced XP (pie × 1.5, rounded to the persisted XP scale like
            // production does), so the equality isn't vacuous.
            Assert.Equal(Math.Round((decimal)(ServerGameConstants.ProficiencyXpPerVictory * 1.5), 3, MidpointRounding.AwayFromZero), live.Xp);
        }

        [Fact]
        public async Task Victory_NotifyFalse_StillCompletesChallengeAndAccruesProficiency_ButSuppressesLivePushes()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: true, modReward: false);

            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Firebolt");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, setup.PlayerId, skill.Id);
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, name: "Fire");
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, skill.Id);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(setup.PlayerId);
            Assert.NotNull(player);
            var enemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(setup.EnemyId, level: 1);
            Assert.NotNull(enemy);

            const double power = 100.0;
            var stats = new BattleStats { SkillStats = { [skill.Id] = new SkillStats { Uses = 1, TotalDamage = power } } };
            stats.AddTypedDamageDealt(EDamageType.Physical, power);

            // Mirrors BattleService.ResolveStaleBattle's offline/switch settlement (#1859): the player has no
            // live socket, so the event carries Notify: false.
            var battleEvent = new BattleCompletedEvent(
                player, enemy, Victory: true, PlayerDied: false, TotalMs: 5000,
                Stats: stats, IsBossBattle: false, ZoneId: player.CurrentZoneId, PlayerRating: power, Notify: false);

            await MakeHandler(scope).HandleAsync(battleEvent, CancellationToken);

            // The underlying statistics/challenge-completion/proficiency-accrual recording still happens
            // regardless of Notify — only the live client pushes are suppressed.
            Assert.Contains(setup.ItemId, player.Inventory.UnlockedItems.Select(u => u.ItemId));
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            Assert.NotEmpty(await progressRepo.GetCompletedChallengeIds(setup.PlayerId));
            var proficiencyRow = Assert.Single(await progressRepo.GetProficiencies(setup.PlayerId));
            Assert.Equal(proficiency.Id, proficiencyRow.ProficiencyId);
            Assert.True(proficiencyRow.Xp > 0);

            Assert.Empty(player.DomainEvents.OfType<ChallengeCompletedEvent>());
            Assert.Empty(player.DomainEvents.OfType<ProficiencyXpGainedEvent>());
        }

        private BattleStatisticsEventHandler MakeHandler(IServiceScope scope) => new(
            scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>(),
            scope.ServiceProvider.GetRequiredService<ChallengeRewardService>(),
            scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>());

        private static BattleCompletedEvent VictoryEvent(Player player, Game.Core.Enemies.Enemy enemy, BattleStats stats, double playerRating) =>
            new(player, enemy, Victory: true, PlayerDied: false, TotalMs: 5000,
                Stats: stats, IsBossBattle: false, ZoneId: player.CurrentZoneId, PlayerRating: playerRating);

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
