using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core;
using Game.Core.Players;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Integration coverage for <see cref="OfflineProgressService"/> (#1042, extracted from <c>BattleService</c>
    /// by #1516): a returning player's away window is replayed, the rewards (exp, levels, stat points, challenge
    /// unlocks) are applied, the away anchor is re-stamped, and a stale in-flight battle is settled first. The
    /// per-battle reward exp is deterministic (the player's power is stationary offline and the enemy is fixed),
    /// so a win pays a fixed amount and the totals can be asserted exactly.
    /// </summary>
    [Collection("Integration")]
    public class OfflineProgressServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        public OfflineProgressServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        // A win pays exactly this: enemy power (Str 50 + End 50 = 100) matched to the seeded player's power
        // (Str 50 + End 50 = 100) gives a difficulty ratio of 1, so DefeatRewards yields the enemy total, 100.
        private const int ExpPerWin = 100;

        [Fact]
        public async Task SimulateOfflineProgress_MultiBattleAway_AppliesExpLevelsAndStatPoints()
        {
            using var scope = CreateScope();
            var setup = await SeedWinningScenarioAsync(scope);

            var (player, state) = await LoadAsync(scope, setup.PlayerId);
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();

            var levelBefore = player.Level;
            // A long-enough absence to win many battles.
            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.True(summary.BattlesWon > 1, "Expected the away window to win multiple battles.");
            Assert.True(summary.HasProgress);
            // Every win pays the same fixed reward, so the total is exact.
            Assert.Equal((long)summary.BattlesWon * ExpPerWin, summary.TotalExp);
            // The exp drove real level gain, and the summary's deltas match the player aggregate.
            Assert.True(player.Level > levelBefore);
            Assert.Equal(player.Level - levelBefore, summary.LevelsGained);
            Assert.Equal(summary.LevelsGained * GameConstants.StatPointsPerLevel, summary.StatPointsGained);
            // The loop was idle-farming the player's current zone.
            Assert.False(summary.AutoChallengeBoss);
            Assert.Equal(setup.ZoneId, summary.ZoneId);
            // The away anchor is re-stamped to ~now, so the next window starts fresh.
            Assert.True(DateTime.UtcNow - player.LastActivity < TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task SimulateOfflineProgress_BelowThreshold_IsNoOpButReanchorsLastActivity()
        {
            using var scope = CreateScope();
            var setup = await SeedWinningScenarioAsync(scope);

            var (player, state) = await LoadAsync(scope, setup.PlayerId);
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();

            var expBefore = player.Exp;
            var levelBefore = player.Level;
            // Under the 5-minute floor: no rewards, just re-anchor.
            player.LastActivity = DateTime.UtcNow.AddMinutes(-1);

            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.False(summary.HasProgress);
            Assert.Equal(0, summary.BattlesWon);
            Assert.Equal(0, summary.BattlesLost);
            Assert.Equal(0, summary.BattlesDrawn);
            Assert.Equal(0, summary.TotalExp);
            Assert.Equal(expBefore, player.Exp);
            Assert.Equal(levelBefore, player.Level);
            // The anchor still advances, so an immediate re-check is a no-op.
            Assert.True(DateTime.UtcNow - player.LastActivity < TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task SimulateOfflineProgress_SecondImmediateCall_IsNoOp()
        {
            using var scope = CreateScope();
            var setup = await SeedWinningScenarioAsync(scope);

            var (player, state) = await LoadAsync(scope, setup.PlayerId);
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();

            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var first = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);
            Assert.True(first.HasProgress);

            // The first claim re-anchored LastActivity to now, so a second immediate claim sees no away time
            // and earns nothing — rewards cannot be double-collected by reconnecting.
            var second = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.False(second.HasProgress);
            Assert.Equal(0, second.TotalExp);
        }

        [Fact]
        public async Task SimulateOfflineProgress_ChallengeCrossedMidWindow_CompletesOnceAndUnlocksReward()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var setup = await SeedWinningScenarioAsync(scope, reload: false);
            var item = await TestDataSeeder.CreateItemAsync(context);
            // A kill-count challenge a multi-battle window easily crosses, rewarding an item.
            var challenge = await TestDataSeeder.CreateChallengeAsync(
                context, challengeTypeId: EChallengeType.EnemiesKilled, progressGoal: 3m, rewardItemId: item.Id);
            await ReloadReferenceCachesAsync();

            var (player, state) = await LoadAsync(scope, setup.PlayerId);
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            // The challenge appears once in the summary, is marked completed in progress, and its reward is
            // unlocked on the player.
            Assert.Single(summary.CompletedChallenges, c => c.ChallengeId == challenge.Id);
            Assert.Contains(challenge.Id, await progressRepo.GetCompletedChallengeIds(setup.PlayerId));
            Assert.Contains(player.Inventory.UnlockedItems, u => u.ItemId == item.Id);
        }

        [Fact]
        public async Task SimulateOfflineProgress_MixedWinLossWindow_CompletesAWinTrackingChallengeCrossedByEarlyWins()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A zone with two deterministic spawns: a weak enemy the player quickly kills (a win) and a deadly,
            // high-HP enemy that shrugs off the player's hits and one-shots them back (a loss). Random spawning
            // mixes the two across the window, so a kill-tracking statistic is touched only in the win battles —
            // exercising the consolidation's reliance on the union of every battle's touched stats rather than
            // just the final battle's (which may be a loss).
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var winEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Weakling",
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var winEnemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, winEnemy.Id, winEnemySkill.Id);
            // Huge Endurance: too much HP to be one-shot and enough Toughness to shrug off the player's hit.
            var lossEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Juggernaut",
                strengthBase: 1m, strengthPerLevel: 0m, enduranceBase: 100_000m, endurancePerLevel: 0m);
            var lossEnemySkill = await TestDataSeeder.CreateSkillAsync(context, "Obliterate", baseDamage: 100_000m, cooldownMs: 500);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, lossEnemy.Id, lossEnemySkill.Id);

            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, winEnemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, lossEnemy.Id);

            var item = await TestDataSeeder.CreateItemAsync(context);
            // A kill-count challenge a single win satisfies, rewarding an item.
            var challenge = await TestDataSeeder.CreateChallengeAsync(
                context, challengeTypeId: EChallengeType.EnemiesKilled, progressGoal: 1m, rewardItemId: item.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);
            await ReloadReferenceCachesAsync();

            var (player, state) = await LoadAsync(scope, playerEntity.Id);
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            // The window genuinely mixed wins and losses.
            Assert.True(summary.BattlesWon > 0, "Expected at least one win in the mixed window.");
            Assert.True(summary.BattlesLost > 0, "Expected at least one loss in the mixed window.");
            // The kill challenge — touched only in win battles — still completes and unlocks its reward,
            // regardless of whether the window's final battle was a win or a loss.
            Assert.Single(summary.CompletedChallenges, c => c.ChallengeId == challenge.Id);
            Assert.Contains(challenge.Id, await progressRepo.GetCompletedChallengeIds(playerEntity.Id));
            Assert.Contains(player.Inventory.UnlockedItems, u => u.ItemId == item.Id);
        }

        [Fact]
        public async Task SimulateOfflineProgress_AccruesProficiencyXpFromFiredSkills()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            // The player's only selected skill trains a fresh proficiency's path (its damage routes there), so
            // every win trains it.
            var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, name: "Smashing", baseXp: 100m, xpGrowth: 2m);
            await TestDataSeeder.LinkSkillToProficiencyAsync(context, proficiency.Id, playerSkill.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);
            await ReloadReferenceCachesAsync();

            var (player, state) = await LoadAsync(scope, playerEntity.Id);
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.True(summary.BattlesWon > 1, "Expected the away window to win multiple battles.");

            var stored = Assert.Single(await progressRepo.GetProficiencies(playerEntity.Id));
            Assert.Equal(proficiency.Id, stored.ProficiencyId);

            // The player's Smash damage trains its path each win, so the window accrues positive XP. The
            // cumulative XP (residual + the consumed thresholds of the curve baseXp 100 / growth 2) is the
            // window's total — it must match the folded summary gain exactly (the "offline == live, folded onto
            // the welcome-back summary" invariant, spike #982 decision 9), not be re-derived from a per-win pie
            // (the effect-based amount is pie × clamp(damage ÷ power), not a flat pie).
            var cumulative = stored.Xp;
            for (var level = 0; level < stored.Level; level++)
            {
                cumulative += (decimal)(100 * Math.Pow(2, level));
            }
            Assert.True(cumulative > 0, "Expected the offline window to accrue proficiency XP.");

            // The summary carries the folded gain — the window's total XP and the final level/residual XP match
            // the persisted state, so the welcome-back gate can report it.
            var gain = Assert.Single(summary.ProficiencyGains);
            Assert.Equal(proficiency.Id, gain.ProficiencyId);
            Assert.Equal(cumulative, gain.XpGained);
            Assert.Equal(stored.Level, gain.NewLevel);
            Assert.Equal(stored.Xp, gain.NewXp);
        }

        [Fact]
        public async Task SimulateOfflineProgress_StaleInFlightBattle_IsResolvedBeforeSimulating()
        {
            using var scope = CreateScope();
            var setup = await SeedWinningScenarioAsync(scope);

            var (player, state) = await LoadAsync(scope, setup.PlayerId);
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();

            // Leave a battle in-flight (a mid-battle disconnect), backdated so its replay resolves.
            await battleService.StartBattle(player, state, zoneId: setup.ZoneId);
            Assert.True(state.HasActiveBattle);
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            // The stale battle is settled and cleared before the away window simulates, so no battle is left
            // active for the idle loop to re-abandon after the gate.
            Assert.False(state.HasActiveBattle);
            Assert.True(summary.HasProgress);
        }

        [Fact]
        public async Task SimulateOfflineProgress_BossMode_RunsTheBossLoop()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A weak, beatable boss so the boss loop produces wins; the player one-shots it.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Weak Boss", isBoss: true,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var bossSkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Boss Zone", levelMin: 1, levelMax: 1, bossEnemyId: boss.Id, bossLevel: 1);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);
            await ReloadReferenceCachesAsync();

            var (player, state) = await LoadAsync(scope, playerEntity.Id);
            // The persisted loop mode is boss-farming.
            player.SetAutoChallengeBoss(true);
            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();
            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.True(summary.AutoChallengeBoss);
            Assert.Equal(zone.Id, summary.ZoneId);
            Assert.True(summary.BattlesWon > 1);
        }

        [Fact]
        public async Task SimulateOfflineProgress_BossModeInBosslessZone_FallsBackToIdleInSameZone()
        {
            using var scope = CreateScope();
            var setup = await SeedWinningScenarioAsync(scope);

            var (player, state) = await LoadAsync(scope, setup.PlayerId);

            // The persisted mode is boss-farming, but the current zone has no dedicated boss (e.g. it was
            // unauthored since the mode was enabled): the loop must fall back to idle rather than stall.
            player.SetAutoChallengeBoss(true);
            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();
            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.False(summary.AutoChallengeBoss);
            // The zone is still viable for idling, so no relocation happens.
            Assert.Equal(setup.ZoneId, summary.ZoneId);
            Assert.True(summary.BattlesWon > 1);
        }

        [Fact]
        public async Task SimulateOfflineProgress_BossModeInRetiredZone_FallsBackToIdleInViableZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The player parked boss-farming in a zone that has since been retired. Its boss is no longer
            // challengeable (out of circulation), so the loop falls back to idle — and the retired zone is
            // not viable either, so the idle fallback relocates to the nearest viable, unlocked zone.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);

            var viableZone = await TestDataSeeder.CreateZoneAsync(context, "Viable", levelMin: 1, levelMax: 1, order: 0);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, viableZone.Id, enemy.Id);

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Boss", isBoss: true);
            var retiredZone = await TestDataSeeder.CreateZoneAsync(
                context, "Retired Boss Zone", levelMin: 1, levelMax: 1, order: 1,
                bossEnemyId: boss.Id, bossLevel: 1, retiredAt: DateTime.UtcNow);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: retiredZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);
            await ReloadReferenceCachesAsync();

            var (player, state) = await LoadAsync(scope, playerEntity.Id);
            player.SetAutoChallengeBoss(true);
            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();
            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.False(summary.AutoChallengeBoss);
            Assert.Equal(viableZone.Id, summary.ZoneId);
            Assert.Equal(viableZone.Id, player.CurrentZoneId);
            Assert.True(summary.BattlesWon > 1);
        }

        [Fact]
        public async Task SimulateOfflineProgress_BossModeInLockedZone_FallsBackToIdleInSameZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The current zone has a boss but is gated behind a challenge the player has not completed, so
            // its boss is not challengeable and the loop falls back to idle. The zone itself is viable
            // (spawnable enemies), so the idle fallback runs in place without relocating.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Boss", isBoss: true);
            var gate = await TestDataSeeder.CreateChallengeAsync(context, "Reach the boss zone");
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Locked Boss Zone", levelMin: 1, levelMax: 1,
                bossEnemyId: boss.Id, bossLevel: 1, unlockChallengeId: gate.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);
            await ReloadReferenceCachesAsync();

            var (player, state) = await LoadAsync(scope, playerEntity.Id);
            player.SetAutoChallengeBoss(true);
            player.LastActivity = DateTime.UtcNow.AddMinutes(-30);

            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();
            var summary = await offlineProgressService.SimulateOfflineProgress(player, state, CancellationToken);

            Assert.False(summary.AutoChallengeBoss);
            Assert.Equal(zone.Id, summary.ZoneId);
            Assert.True(summary.BattlesWon > 1);
        }

        [Fact]
        public async Task SimulateSwitchProgress_SubThresholdAway_StillCreditsAndReanchors()
        {
            // A deliberate character switch drops the 5-minute floor: an away window the login path would treat
            // as a no-op still credits the departed character (#922 lossless switch).
            using var scope = CreateScope();
            var setup = await SeedWinningScenarioAsync(scope);

            var (player, state) = await LoadAsync(scope, setup.PlayerId);
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();

            var levelBefore = player.Level;
            // Under the 5-minute login floor, but long enough to win several battles when the floor is dropped.
            player.LastActivity = DateTime.UtcNow.AddMinutes(-2);

            var summary = await offlineProgressService.SimulateSwitchProgress(player, state, CancellationToken);

            Assert.True(summary.BattlesWon > 1, "Expected the sub-threshold window to still win multiple battles.");
            Assert.True(summary.HasProgress);
            Assert.Equal((long)summary.BattlesWon * ExpPerWin, summary.TotalExp);
            Assert.True(player.Level > levelBefore);
            // The away anchor is re-stamped to ~now, so the parked window starts fresh from the switch.
            Assert.True(DateTime.UtcNow - player.LastActivity < TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task SimulateSwitchProgress_StaleInFlightBattle_IsResolvedBeforeSimulating()
        {
            // The departed character's in-flight battle must be settled by the switch credit (not discarded),
            // even for a sub-threshold away window the login path would skip.
            using var scope = CreateScope();
            var setup = await SeedWinningScenarioAsync(scope);

            var (player, state) = await LoadAsync(scope, setup.PlayerId);
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var offlineProgressService = scope.ServiceProvider.GetRequiredService<OfflineProgressService>();

            await battleService.StartBattle(player, state, zoneId: setup.ZoneId);
            Assert.True(state.HasActiveBattle);
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            // Sub-threshold away — the login path would leave the stale battle for the next StartBattle; the
            // switch path resolves it.
            player.LastActivity = DateTime.UtcNow.AddMinutes(-2);

            await offlineProgressService.SimulateSwitchProgress(player, state, CancellationToken);

            Assert.False(state.HasActiveBattle);
        }

        /// <summary>
        /// Seeds a player who reliably one-shots a fixed-power enemy in a single-zone idle loop, so an away
        /// window produces a deterministic run of victories (each worth <see cref="ExpPerWin"/>). The enemy's
        /// power equals the seeded player's, so the exp reward is exactly the enemy total.
        /// </summary>
        private async Task<Setup> SeedWinningScenarioAsync(IServiceScope scope, bool reload = true)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            if (reload)
            {
                await ReloadReferenceCachesAsync();
            }

            return new Setup(playerEntity.Id, zone.Id);
        }

        private async Task<(Player Player, PlayerState State)> LoadAsync(IServiceScope scope, int playerId)
        {
            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerId);
            Assert.NotNull(player);
            return (player, new PlayerState { PlayerId = playerId });
        }

        private sealed record Setup(int PlayerId, int ZoneId);
    }
}
