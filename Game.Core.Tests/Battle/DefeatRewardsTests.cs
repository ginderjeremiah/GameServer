using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Core.Tests.Battle
{
    [TestClass]
    public class DefeatRewardsTests
    {
        [TestMethod]
        public void ExpReward_ZeroPlayerStats_ReturnsFlooredEnemyTotal()
        {
            var player = MakePlayer(statPointsGained: 0);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player, enemy, new Mulberry32(42u));

            // Enemy attribute total = 10 + 5 = 15. Player stat total = 0 → floor(15) = 15.
            Assert.AreEqual(15, rewards.ExpReward);
        }

        [TestMethod]
        public void ExpReward_EqualStrength_MultiplierIsOne()
        {
            var player = MakePlayer(statPointsGained: 15);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player, enemy, new Mulberry32(42u));

            // ratio = 15/15 = 1.0; within [0.8, 1.2] → multiplier = 1.0 → exp = floor(15*1) = 15
            Assert.AreEqual(15, rewards.ExpReward);
        }

        [TestMethod]
        public void ExpReward_RatioNearOne_MultiplierIsOne()
        {
            var player = MakePlayer(statPointsGained: 16);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player, enemy, new Mulberry32(42u));

            // ratio = 15/16 ≈ 0.9375; within [0.8, 1.2] → multiplier = 1.0 → exp = floor(15) = 15
            Assert.AreEqual(15, rewards.ExpReward);
        }

        [TestMethod]
        public void ExpReward_WeakEnemy_ReducedExp()
        {
            var player = MakePlayer(statPointsGained: 100);
            var enemy = MakeEnemy(strength: 5, endurance: 5);

            var rewards = new DefeatRewards(player, enemy, new Mulberry32(42u));

            // ratio = 10/100 = 0.1; < 0.8 → multiplier = 0.1^2 = 0.01 → exp = floor(10 * 0.01) = 0
            Assert.AreEqual(0, rewards.ExpReward);
        }

        [TestMethod]
        public void ExpReward_StrongEnemy_IncreasedExp()
        {
            var player = MakePlayer(statPointsGained: 10);
            var enemy = MakeEnemy(strength: 50, endurance: 50);

            var rewards = new DefeatRewards(player, enemy, new Mulberry32(42u));

            // ratio = 100/10 = 10; > 1.2 → multiplier = 10^2 = 100 → exp = floor(100 * 100) = 10000
            Assert.AreEqual(10000, rewards.ExpReward);
        }

        [TestMethod]
        public void Drops_NoDrops_ReturnsEmpty()
        {
            var player = MakePlayer(statPointsGained: 10);
            var enemy = MakeEnemy(strength: 5, endurance: 5, drops: []);

            var rewards = new DefeatRewards(player, enemy, new Mulberry32(42u));

            Assert.AreEqual(0, rewards.Drops.Count());
        }

        [TestMethod]
        public void Drops_GuaranteedDrop_ReturnsItem()
        {
            var item = new Item
            {
                Id = 1, Name = "Sword", Description = "",
                Category = EItemCategory.Weapon, Attributes = [], ModSlots = [], Tags = [],
            };
            var drops = new List<EnemyDrop>
            {
                new() { Item = item, DropRate = decimal.MaxValue },
            };
            var player = MakePlayer(statPointsGained: 10);
            var enemy = MakeEnemy(strength: 5, endurance: 5, drops: drops);

            var rewards = new DefeatRewards(player, enemy, new Mulberry32(42u));

            Assert.AreEqual(1, rewards.Drops.Count());
            Assert.AreEqual(item, rewards.Drops.First());
        }

        [TestMethod]
        public void Drops_DeterministicSeed_ProducesConsistentResults()
        {
            var item = new Item
            {
                Id = 1, Name = "Sword", Description = "",
                Category = EItemCategory.Weapon, Attributes = [], ModSlots = [], Tags = [],
            };
            var drops = new List<EnemyDrop>
            {
                new() { Item = item, DropRate = 0.5m },
            };
            var player = MakePlayer(statPointsGained: 10);
            var enemy1 = MakeEnemy(strength: 5, endurance: 5, drops: drops);
            var enemy2 = MakeEnemy(strength: 5, endurance: 5, drops: drops);

            var rewards1 = new DefeatRewards(player, enemy1, new Mulberry32(99u));
            var rewards2 = new DefeatRewards(player, enemy2, new Mulberry32(99u));

            Assert.AreEqual(rewards1.Drops.Count(), rewards2.Drops.Count());
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
        };

        private static Enemy MakeEnemy(double strength, double endurance, List<EnemyDrop>? drops = null) => new()
        {
            Id = 1,
            Name = "Test Enemy",
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
            Skills = [],
            Drops = drops ?? [],
        };
    }
}
