using Game.Core;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Progress;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class PlayerProgressTests
    {
        // ── RecordBattleCompleted: global statistics ─────────────────────────

        [Fact]
        public void RecordBattleCompleted_TracksDamageStatistics()
        {
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                PlayerDamageDealt = 120.5,
                PlayerDamageTaken = 40.0,
                HighestPlayerAttack = 33.25,
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 4000, stats);

            Assert.Equal(120.5m, progress.GetStatisticValue(EStatisticType.DamageDealt, null));
            Assert.Equal(40.0m, progress.GetStatisticValue(EStatisticType.DamageTaken, null));
            Assert.Equal(33.25m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, null));
        }

        [Fact]
        public void RecordBattleCompleted_TracksEncountersGloballyAndPerEnemy()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 7);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 1000, new BattleStats());

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesEncountered, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesEncountered, 7));
        }

        [Fact]
        public void RecordBattleCompleted_RecordsBattleTimeAndSkillsUsedInSeconds()
        {
            var progress = MakeProgress();
            var stats = new BattleStats { PlayerSkillsUsed = 5 };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 4500, stats);

            // TotalBattleTime is tracked in seconds (ms / 1000).
            Assert.Equal(4.5m, progress.GetStatisticValue(EStatisticType.TotalBattleTime, null));
            Assert.Equal(5m, progress.GetStatisticValue(EStatisticType.SkillsUsed, null));
        }

        // ── RecordBattleCompleted: victory vs. loss ──────────────────────────

        [Fact]
        public void RecordBattleCompleted_Victory_TracksWinKillAndFastestVictory()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 3, isBoss: false);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 6000, new BattleStats());

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesWon, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesWon, 3));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, 3));
            // FastestVictory is recorded in seconds, globally and per-enemy.
            Assert.Equal(6m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
            Assert.Equal(6m, progress.GetStatisticValue(EStatisticType.FastestVictory, 3));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BattlesLost, null));
        }

        [Fact]
        public void RecordBattleCompleted_Loss_TracksLossButNotKillOrVictory()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 3);

            progress.RecordBattleCompleted(enemy, victory: false, playerDied: false, totalMs: 6000, new BattleStats());

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesLost, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesLost, 3));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BattlesWon, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
        }

        [Fact]
        public void RecordBattleCompleted_BossVictory_IncrementsBossesDefeated()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(isBoss: true), victory: true, playerDied: false, totalMs: 1000, new BattleStats());

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BossesDefeated, null));
        }

        [Fact]
        public void RecordBattleCompleted_NonBossVictory_DoesNotIncrementBossesDefeated()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(isBoss: false), victory: true, playerDied: false, totalMs: 1000, new BattleStats());

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BossesDefeated, null));
        }

        [Fact]
        public void RecordBattleCompleted_BossLoss_DoesNotIncrementBossesDefeated()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(isBoss: true), victory: false, playerDied: true, totalMs: 1000, new BattleStats());

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BossesDefeated, null));
        }

        // ── RecordBattleCompleted: zone clears (boss victory) ────────────────

        [Fact]
        public void RecordBattleCompleted_BossVictory_IncrementsZonesClearedGloballyAndForCurrentZone()
        {
            var progress = MakeProgress(player: MakePlayer(currentZoneId: 4));

            progress.RecordBattleCompleted(MakeEnemy(isBoss: true), victory: true, playerDied: false, totalMs: 1000, new BattleStats());

            // A boss victory clears the player's current zone, tracked globally and per-zone.
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 4));
        }

        [Fact]
        public void RecordBattleCompleted_NonBossVictory_DoesNotIncrementZonesCleared()
        {
            var progress = MakeProgress(player: MakePlayer(currentZoneId: 4));

            progress.RecordBattleCompleted(MakeEnemy(isBoss: false), victory: true, playerDied: false, totalMs: 1000, new BattleStats());

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 4));
        }

        [Fact]
        public void RecordBattleCompleted_BossLoss_DoesNotIncrementZonesCleared()
        {
            var progress = MakeProgress(player: MakePlayer(currentZoneId: 4));

            progress.RecordBattleCompleted(MakeEnemy(isBoss: true), victory: false, playerDied: true, totalMs: 1000, new BattleStats());

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 4));
        }

        [Fact]
        public void RecordBattleCompleted_RepeatedBossVictoriesInSameZone_AccumulateZonesCleared()
        {
            var progress = MakeProgress(player: MakePlayer(currentZoneId: 2));

            progress.RecordBattleCompleted(MakeEnemy(isBoss: true), victory: true, playerDied: false, totalMs: 1000, new BattleStats());
            progress.RecordBattleCompleted(MakeEnemy(isBoss: true), victory: true, playerDied: false, totalMs: 1000, new BattleStats());

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 2));
        }

        [Fact]
        public void RecordBattleCompleted_PlayerDied_IncrementsPlayerDeaths()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: false, playerDied: true, totalMs: 1000, new BattleStats());

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.PlayerDeaths, null));
        }

        [Fact]
        public void RecordBattleCompleted_PlayerSurvived_DoesNotIncrementPlayerDeaths()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, new BattleStats());

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.PlayerDeaths, null));
        }

        // ── RecordBattleCompleted: per-skill statistics ──────────────────────

        [Fact]
        public void RecordBattleCompleted_TracksPerSkillStatistics()
        {
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                PlayerSkillsUsed = 3,
                SkillStats =
                {
                    [10] = new SkillStats { SkillId = 10, Uses = 2, TotalDamage = 60.0, HighestSingleAttack = 35.0 },
                    [20] = new SkillStats { SkillId = 20, Uses = 1, TotalDamage = 15.0, HighestSingleAttack = 15.0 },
                },
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats);

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.SkillsUsed, 10));
            Assert.Equal(60m, progress.GetStatisticValue(EStatisticType.DamageDealt, 10));
            Assert.Equal(35m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, 10));

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.SkillsUsed, 20));
            Assert.Equal(15m, progress.GetStatisticValue(EStatisticType.DamageDealt, 20));
        }

        // ── RecordBattleCompleted: accumulation across battles ───────────────

        [Fact]
        public void RecordBattleCompleted_AccumulatesAdditiveStatisticsAcrossBattles()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 1);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 2000,
                new BattleStats { PlayerDamageDealt = 50.0 });
            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 3000,
                new BattleStats { PlayerDamageDealt = 70.0 });

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, null));
            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.EnemiesEncountered, null));
            Assert.Equal(120m, progress.GetStatisticValue(EStatisticType.DamageDealt, null));
            Assert.Equal(5m, progress.GetStatisticValue(EStatisticType.TotalBattleTime, null));
        }

        [Fact]
        public void RecordBattleCompleted_KeepsMaximumHighestSingleAttack()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { HighestPlayerAttack = 40.0 });
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { HighestPlayerAttack = 25.0 });

            Assert.Equal(40m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, null));
        }

        [Fact]
        public void RecordBattleCompleted_KeepsMinimumFastestVictory()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 1);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 5000, new BattleStats());
            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 3000, new BattleStats());
            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 8000, new BattleStats());

            Assert.Equal(3m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
        }

        // ── GetStatisticValue ────────────────────────────────────────────────

        [Fact]
        public void GetStatisticValue_UntrackedStatistic_ReturnsZero()
        {
            var progress = MakeProgress();

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.DamageDealt, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, 999));
        }

        // ── EvaluateChallenges ───────────────────────────────────────────────

        [Fact]
        public void EvaluateChallenges_StatisticMeetsGoal_CompletesAndReturnsReward()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5, rewardItemId: 42, rewardItemModId: 7);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 5m),
            ]);

            var completed = progress.EvaluateChallenges([challenge]);

            var result = Assert.Single(completed);
            Assert.Equal(0, result.ChallengeId);
            Assert.Equal(42, result.RewardItemId);
            Assert.Equal(7, result.RewardItemModId);

            var playerChallenge = Assert.Single(progress.ChallengeProgress);
            Assert.True(playerChallenge.Completed);
            Assert.NotNull(playerChallenge.CompletedAt);
            Assert.Equal(5m, playerChallenge.Progress);
        }

        [Fact]
        public void EvaluateChallenges_StatisticBelowGoal_TracksProgressWithoutCompleting()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 10);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 3m),
            ]);

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Empty(completed);
            var playerChallenge = Assert.Single(progress.ChallengeProgress);
            Assert.False(playerChallenge.Completed);
            Assert.Null(playerChallenge.CompletedAt);
            Assert.Equal(3m, playerChallenge.Progress);
        }

        [Fact]
        public void EvaluateChallenges_StatisticExceedsGoal_ClampsProgressToGoal()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 12m),
            ]);

            progress.EvaluateChallenges([challenge]);

            var playerChallenge = Assert.Single(progress.ChallengeProgress);
            Assert.True(playerChallenge.Completed);
            Assert.Equal(5m, playerChallenge.Progress);
        }

        [Fact]
        public void EvaluateChallenges_AlreadyCompletedChallenge_IsNotReReported()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var alreadyCompleted = new PlayerChallenge(challenge, progress: 5m, completed: true, completedAt: DateTime.UtcNow);
            var progress = MakeProgress(
                statistics: [Stat(EStatisticType.EnemiesKilled, null, 12m)],
                challenges: [alreadyCompleted]);

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Empty(completed);
        }

        [Fact]
        public void EvaluateChallenges_PerEntityChallenge_UsesPerEntityStatistic()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 3, targetEntityId: 2);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 100m), // global total should be ignored
                Stat(EStatisticType.EnemiesKilled, 2, 3m),
            ]);

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Single(completed);
        }

        [Fact]
        public void EvaluateChallenges_ZonesCleared_CompletesAfterBossVictoryInTargetZone()
        {
            // Regression test for the bug where ZonesCleared was never written, so a
            // ZonesCleared challenge could never complete. A boss victory in the target zone
            // should now satisfy it.
            var challenge = MakeChallenge(id: 0, EChallengeType.ZonesCleared, goal: 1, targetEntityId: 3);
            var progress = MakeProgress(player: MakePlayer(currentZoneId: 3));

            progress.RecordBattleCompleted(MakeEnemy(isBoss: true), victory: true, playerDied: false, totalMs: 1000, new BattleStats());
            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Single(completed);
            Assert.True(Assert.Single(progress.ChallengeProgress).Completed);
        }

        [Fact]
        public void EvaluateChallenges_LevelReached_UsesPlayerLevel()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.LevelReached, goal: 5);
            var progress = MakeProgress(player: MakePlayer(level: 5));

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Single(completed);
        }

        [Fact]
        public void EvaluateChallenges_LevelReachedBelowGoal_DoesNotComplete()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.LevelReached, goal: 10);
            var progress = MakeProgress(player: MakePlayer(level: 5));

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Empty(completed);
            Assert.Equal(5m, Assert.Single(progress.ChallengeProgress).Progress);
        }

        [Fact]
        public void EvaluateChallenges_TimeTrial_CompletesWhenFastestVictoryAtOrBelowGoal()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.TimeTrial, goal: 10);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.FastestVictory, null, 8m), // best victory in 8s, goal is "within 10s"
            ]);

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Single(completed);
            Assert.True(Assert.Single(progress.ChallengeProgress).Completed);
        }

        [Fact]
        public void EvaluateChallenges_TimeTrial_DoesNotCompleteWhenFastestVictoryAboveGoal()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.TimeTrial, goal: 10);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.FastestVictory, null, 14m),
            ]);

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Empty(completed);
        }

        [Fact]
        public void EvaluateChallenges_TimeTrial_DoesNotCompleteWithNoRecordedVictory()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.TimeTrial, goal: 10);
            var progress = MakeProgress(); // no FastestVictory statistic recorded yet

            var completed = progress.EvaluateChallenges([challenge]);

            Assert.Empty(completed);
        }

        [Fact]
        public void EvaluateChallenges_MultipleChallenges_ReturnsOnlyCompletedOnes()
        {
            var met = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var unmet = MakeChallenge(id: 1, EChallengeType.BattlesWon, goal: 100);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 5m),
                Stat(EStatisticType.BattlesWon, null, 2m),
            ]);

            var completed = progress.EvaluateChallenges([met, unmet]);

            Assert.Equal(0, Assert.Single(completed).ChallengeId);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static PlayerProgress MakeProgress(
            Player? player = null,
            IEnumerable<PlayerStatistic>? statistics = null,
            IEnumerable<PlayerChallenge>? challenges = null)
        {
            return new PlayerProgress(player ?? MakePlayer(), statistics ?? [], challenges ?? []);
        }

        private static PlayerStatistic Stat(EStatisticType type, int? entityId, decimal value) =>
            new() { Type = type, EntityId = entityId, Value = value };

        private static Challenge MakeChallenge(
            int id,
            EChallengeType type,
            decimal goal,
            int? targetEntityId = null,
            int? rewardItemId = null,
            int? rewardItemModId = null) => new()
            {
                Id = id,
                Name = "Test Challenge",
                Description = string.Empty,
                Type = new ChallengeType(type),
                TargetEntityId = targetEntityId,
                ProgressGoal = goal,
                RewardItemId = rewardItemId,
                RewardItemModId = rewardItemModId,
            };

        private static Enemy MakeEnemy(int id = 1, bool isBoss = false) => new()
        {
            Id = id,
            Name = "Test Enemy",
            Level = 1,
            IsBoss = isBoss,
            AttributeDistributions = [],
            Skills = [],
        };

        private static Player MakePlayer(int level = 1, int currentZoneId = 0) => new()
        {
            Id = 1,
            Name = "Test",
            Level = level,
            Exp = 0,
            CurrentZoneId = currentZoneId,
            StatPoints = new PlayerStatPoints([]) { StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
            LogPreferences = [],
        };
    }
}
