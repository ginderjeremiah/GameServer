using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Core.Tests.Battle
{
    public class DefeatRewardsTests
    {
        [Fact]
        public void ExpReward_ZeroPlayerStats_ReturnsFlooredEnemyTotal()
        {
            var player = MakePlayer(statPointsGained: 0);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player, enemy);

            // Enemy attribute total = 10 + 5 = 15. Player stat total = 0 → floor(15) = 15.
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_EqualStrength_MultiplierIsOne()
        {
            var player = MakePlayer(statPointsGained: 15);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player, enemy);

            // ratio = 15/15 = 1.0; within [0.8, 1.2] → multiplier = 1.0 → exp = floor(15*1) = 15
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_RatioNearOne_MultiplierIsOne()
        {
            var player = MakePlayer(statPointsGained: 16);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player, enemy);

            // ratio = 15/16 ≈ 0.9375; within [0.8, 1.2] → multiplier = 1.0 → exp = floor(15) = 15
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_WeakEnemy_ReducedExp()
        {
            var player = MakePlayer(statPointsGained: 100);
            var enemy = MakeEnemy(strength: 5, endurance: 5);

            var rewards = new DefeatRewards(player, enemy);

            // ratio = 10/100 = 0.1; < 0.8 → multiplier = 0.1^2 = 0.01 → exp = floor(10 * 0.01) = 0
            Assert.Equal(0, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_StrongEnemy_IncreasedExp()
        {
            var player = MakePlayer(statPointsGained: 10);
            var enemy = MakeEnemy(strength: 50, endurance: 50);

            var rewards = new DefeatRewards(player, enemy);

            // ratio = 100/10 = 10; > 1.2 → multiplier = 10^2 = 100 → exp = floor(100 * 100) = 10000
            Assert.Equal(10000, rewards.ExpReward);
        }

        private static Player MakePlayer(int statPointsGained) => new()
        {
            Id = 1,
            Name = "Test",
            Level = 1,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([])
            { StatPointsGained = statPointsGained, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
            LogPreferences = [],
        };

        private static Enemy MakeEnemy(double strength, double endurance) => new()
        {
            Id = 1,
            Name = "Test Enemy",
            IsBoss = false,
            Level = 1,
            AttributeDistributions =
            [
                new AttributeDistribution
                {
                    AttributeId = EAttribute.Strength,
                    BaseAmount = (decimal)strength,
                    AmountPerLevel = 0,
                },
                new AttributeDistribution
                {
                    AttributeId = EAttribute.Endurance,
                    BaseAmount = (decimal)endurance,
                    AmountPerLevel = 0,
                },
            ],
            AvailableSkills = [],
        };
    }
}
