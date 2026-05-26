using Game.Core.Battle;
using Game.Core.Players;

namespace Game.Core.Tests.Players
{
    [TestClass]
    public class PlayerStateTests
    {
        [TestMethod]
        public void SetActiveBattle_SetsAllFields()
        {
            var state = new PlayerState();
            var startTime = DateTime.UtcNow;
            var snapshot = MakeSnapshot();

            state.SetActiveBattle(enemyId: 7, level: 3, seed: 42u, startTime: startTime, snapshot: snapshot);

            Assert.AreEqual(7, state.ActiveEnemyId);
            Assert.AreEqual(3, state.ActiveEnemyLevel);
            Assert.AreEqual(42u, state.BattleSeed);
            Assert.AreEqual(startTime, state.BattleStartTime);
            Assert.AreSame(snapshot, state.Snapshot);
        }

        [TestMethod]
        public void HasActiveBattle_NoActiveBattle_ReturnsFalse()
        {
            var state = new PlayerState();

            Assert.IsFalse(state.HasActiveBattle);
        }

        [TestMethod]
        public void HasActiveBattle_WithActiveBattle_ReturnsTrue()
        {
            var state = new PlayerState();
            state.SetActiveBattle(1, 1, 0u, DateTime.UtcNow, MakeSnapshot());

            Assert.IsTrue(state.HasActiveBattle);
        }

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

        [TestMethod]
        public void ClearBattle_ResetsActiveFields()
        {
            var state = new PlayerState();
            state.SetActiveBattle(5, 2, 99u, DateTime.UtcNow, MakeSnapshot());

            state.ClearBattle();

            Assert.IsNull(state.ActiveEnemyId);
            Assert.IsNull(state.ActiveEnemyLevel);
            Assert.IsNull(state.BattleSeed);
            Assert.AreEqual(DateTime.UnixEpoch, state.BattleStartTime);
            Assert.IsNull(state.Snapshot);
            Assert.IsFalse(state.HasActiveBattle);
        }

        [TestMethod]
        public void ClearBattle_DoesNotAffectCooldown()
        {
            var state = new PlayerState();
            var cooldown = DateTime.UtcNow.AddSeconds(5);
            state.SetActiveBattle(5, 2, 99u, DateTime.UtcNow, MakeSnapshot());
            state.SetCooldown(cooldown);

            state.ClearBattle();

            Assert.AreEqual(cooldown, state.EnemyCooldown);
        }

        private static BattleSnapshot MakeSnapshot() => new()
        {
            Level = 1,
            StatAllocations = [],
            EquippedItems = [],
            SkillIds = [],
        };
    }
}
