using Game.Core.Battle;
using Game.Core.Players;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class PlayerStateTests
    {
        [Fact]
        public void SetActiveBattle_SetsAllFields()
        {
            var state = new PlayerState();
            var startTime = DateTime.UtcNow;
            var snapshot = MakeSnapshot();
            var enemySkillIds = new List<int> { 1, 2, 3 };

            state.SetActiveBattle(enemyId: 7, level: 3, enemySkillIds: enemySkillIds, seed: 42u, startTime: startTime, snapshot: snapshot);

            Assert.Equal(7, state.ActiveEnemyId);
            Assert.Equal(3, state.ActiveEnemyLevel);
            Assert.Same(enemySkillIds, state.ActiveEnemySkillIds);
            Assert.Equal(42u, state.BattleSeed);
            Assert.Equal(startTime, state.BattleStartTime);
            Assert.Same(snapshot, state.Snapshot);
        }

        [Fact]
        public void HasActiveBattle_NoActiveBattle_ReturnsFalse()
        {
            var state = new PlayerState();

            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public void HasActiveBattle_WithActiveBattle_ReturnsTrue()
        {
            var state = new PlayerState();
            state.SetActiveBattle(1, 1, [], 0u, DateTime.UtcNow, MakeSnapshot());

            Assert.True(state.HasActiveBattle);
        }

        [Fact]
        public void IsOnCooldown_WhenCooldownIsInFuture_ReturnsTrue()
        {
            var state = new PlayerState();
            state.SetCooldown(DateTime.UtcNow.AddSeconds(10));

            Assert.True(state.IsOnCooldown(DateTime.UtcNow));
        }

        [Fact]
        public void IsOnCooldown_WhenCooldownHasPassed_ReturnsFalse()
        {
            var state = new PlayerState();
            state.SetCooldown(DateTime.UtcNow.AddSeconds(-10));

            Assert.False(state.IsOnCooldown(DateTime.UtcNow));
        }

        [Fact]
        public void ClearBattle_ResetsActiveFields()
        {
            var state = new PlayerState();
            state.SetActiveBattle(5, 2, [10, 11], 99u, DateTime.UtcNow, MakeSnapshot());

            state.ClearBattle();

            Assert.Null(state.ActiveEnemyId);
            Assert.Null(state.ActiveEnemyLevel);
            Assert.Null(state.ActiveEnemySkillIds);
            Assert.Null(state.BattleSeed);
            Assert.Equal(DateTime.UnixEpoch, state.BattleStartTime);
            Assert.Null(state.Snapshot);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public void ClearBattle_DoesNotAffectCooldown()
        {
            var state = new PlayerState();
            var cooldown = DateTime.UtcNow.AddSeconds(5);
            state.SetActiveBattle(5, 2, [], 99u, DateTime.UtcNow, MakeSnapshot());
            state.SetCooldown(cooldown);

            state.ClearBattle();

            Assert.Equal(cooldown, state.EnemyCooldown);
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
