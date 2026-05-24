using Game.Core.Players;

namespace Game.Core.Tests.Players
{
    [TestClass]
    public class PlayerStateTests
    {
        // ── SetActiveBattle ──────────────────────────────────────────────────

        [TestMethod]
        public void SetActiveBattle_SetsAllFields()
        {
            var state = new PlayerState();
            var earliest = DateTime.UtcNow.AddSeconds(5);

            state.SetActiveBattle(enemyId: 7, level: 3, seed: 42u, earliestDefeat: earliest, victory: true);

            Assert.AreEqual(7, state.ActiveEnemyId);
            Assert.AreEqual(3, state.ActiveEnemyLevel);
            Assert.AreEqual(42u, state.BattleSeed);
            Assert.AreEqual(earliest, state.EarliestDefeat);
            Assert.IsTrue(state.Victory);
        }

        // ── CanDefeatEnemy ───────────────────────────────────────────────────

        [TestMethod]
        public void CanDefeatEnemy_NoActiveBattle_ReturnsFalse()
        {
            var state = new PlayerState { Victory = true };
            // ActiveEnemyId is null by default

            Assert.IsFalse(state.CanDefeatEnemy(DateTime.UtcNow));
        }

        [TestMethod]
        public void CanDefeatEnemy_NotVictory_ReturnsFalse()
        {
            var state = new PlayerState();
            state.SetActiveBattle(1, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: false);

            Assert.IsFalse(state.CanDefeatEnemy(DateTime.UtcNow));
        }

        [TestMethod]
        public void CanDefeatEnemy_BeforeEarliestDefeat_ReturnsFalse()
        {
            var state = new PlayerState();
            state.SetActiveBattle(1, 1, 0u, DateTime.UtcNow.AddSeconds(60), victory: true);

            Assert.IsFalse(state.CanDefeatEnemy(DateTime.UtcNow));
        }

        [TestMethod]
        public void CanDefeatEnemy_VictoryAndTimeElapsed_ReturnsTrue()
        {
            var state = new PlayerState();
            state.SetActiveBattle(1, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            Assert.IsTrue(state.CanDefeatEnemy(DateTime.UtcNow));
        }

        // ── Cooldown ─────────────────────────────────────────────────────────

        [TestMethod]
        public void IsOnCooldown_WhenCooldownIsInFuture_ReturnsTrue()
        {
            var state = new PlayerState();
            state.SetCooldown(DateTime.UtcNow.AddSeconds(10));

            Assert.IsTrue(state.IsOnCooldown(DateTime.UtcNow));
        }

        [TestMethod]
        public void IsOnCooldown_WhenCooldownHasPassed_ReturnsFalse()
        {
            var state = new PlayerState();
            state.SetCooldown(DateTime.UtcNow.AddSeconds(-10));

            Assert.IsFalse(state.IsOnCooldown(DateTime.UtcNow));
        }

        // ── ClearBattle ──────────────────────────────────────────────────────

        [TestMethod]
        public void ClearBattle_ResetsActiveFields()
        {
            var state = new PlayerState();
            state.SetActiveBattle(5, 2, 99u, DateTime.UtcNow.AddSeconds(3), victory: true);

            state.ClearBattle();

            Assert.IsNull(state.ActiveEnemyId);
            Assert.IsNull(state.ActiveEnemyLevel);
            Assert.IsNull(state.BattleSeed);
            Assert.AreEqual(DateTime.UnixEpoch, state.EarliestDefeat);
            Assert.IsFalse(state.Victory);
        }

        [TestMethod]
        public void ClearBattle_DoesNotAffectCooldown()
        {
            var state = new PlayerState();
            var cooldown = DateTime.UtcNow.AddSeconds(5);
            state.SetActiveBattle(5, 2, 99u, DateTime.UtcNow.AddSeconds(-1), victory: true);
            state.SetCooldown(cooldown);

            state.ClearBattle();

            Assert.AreEqual(cooldown, state.EnemyCooldown);
        }
    }
}
