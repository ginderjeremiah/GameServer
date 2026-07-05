using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Battle.Offline;
using Game.Core.Enemies;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Battle.Offline
{
    /// <summary>
    /// Focused coverage of the run-level aggregation the result folds out of its per-battle outcomes,
    /// constructed directly so the win/loss/draw classification and reward totals are pinned without
    /// depending on the battle simulation.
    /// </summary>
    public class OfflineProgressResultTests
    {
        [Fact]
        public void EmptyBattles_AllAggregatesZero()
        {
            var result = new OfflineProgressResult(OfflineLoopMode.Idle, zoneId: 3, battles: []);

            Assert.Equal(0, result.BattlesSimulated);
            Assert.Equal(0, result.Wins);
            Assert.Equal(0, result.Losses);
            Assert.Equal(0, result.Draws);
            Assert.Equal(0, result.TotalExp);
        }

        [Fact]
        public void ClassifiesWinLossAndDraw_ByVictoryAndPlayerDiedFlags()
        {
            var battles = new List<OfflineBattleOutcome>
            {
                Win(enemyId: 1, totalMs: 40, exp: 10),
                Loss(enemyId: 1, totalMs: 200),
                Draw(enemyId: 2, totalMs: 120_000),
            };

            var result = new OfflineProgressResult(OfflineLoopMode.Boss, zoneId: 5, battles);

            Assert.Equal(3, result.BattlesSimulated);
            Assert.Equal(1, result.Wins);
            Assert.Equal(1, result.Losses);
            Assert.Equal(1, result.Draws);
        }

        [Fact]
        public void TotalExp_SumsOnlyVictoryRewards()
        {
            var battles = new List<OfflineBattleOutcome>
            {
                Win(enemyId: 1, totalMs: 40, exp: 7),
                Win(enemyId: 1, totalMs: 40, exp: 11),
                Loss(enemyId: 1, totalMs: 80), // a loss carries no exp even if one were supplied
                Draw(enemyId: 1, totalMs: 120_000),
            };

            var result = new OfflineProgressResult(OfflineLoopMode.Idle, zoneId: 1, battles);

            Assert.Equal(18, result.TotalExp);
        }

        [Fact]
        public void IsBossBattle_DerivedFromMode()
        {
            Assert.True(new OfflineProgressResult(OfflineLoopMode.Boss, 1, []).IsBossBattle);
            Assert.False(new OfflineProgressResult(OfflineLoopMode.Idle, 1, []).IsBossBattle);
        }

        [Fact]
        public void RemainderMs_DefaultsToZero_WhenNotSupplied()
        {
            var result = new OfflineProgressResult(OfflineLoopMode.Idle, zoneId: 1, battles: []);

            Assert.Equal(0, result.RemainderMs);
        }

        [Fact]
        public void RemainderMs_CarriesTheSuppliedValue()
        {
            var result = new OfflineProgressResult(OfflineLoopMode.Idle, zoneId: 1, battles: [], remainderMs: 4200);

            Assert.Equal(4200, result.RemainderMs);
        }

        // ── Builders ─────────────────────────────────────────────────────────

        private static OfflineBattleOutcome Win(int enemyId, int totalMs, int exp) =>
            new(MakeEnemy(enemyId), new BattleResult(Victory: true, PlayerDied: false, totalMs, new BattleStats()), exp, PlayerRating: 100.0, EnemyRating: 50.0);

        private static OfflineBattleOutcome Loss(int enemyId, int totalMs) =>
            new(MakeEnemy(enemyId), new BattleResult(Victory: false, PlayerDied: true, totalMs, new BattleStats()), ExpReward: 0, PlayerRating: 0, EnemyRating: 0);

        private static OfflineBattleOutcome Draw(int enemyId, int totalMs) =>
            new(MakeEnemy(enemyId), new BattleResult(Victory: false, PlayerDied: false, totalMs, new BattleStats()), ExpReward: 0, PlayerRating: 0, EnemyRating: 0);

        private static Enemy MakeEnemy(int id) => new()
        {
            Id = id,
            Name = $"Enemy {id}",
            IsBoss = false,
            Level = 1,
            AttributeDistributions = [new AttributeDistribution { AttributeId = Strength, BaseAmount = 1, AmountPerLevel = 0 }],
            AvailableSkills = [],
        };
    }
}
