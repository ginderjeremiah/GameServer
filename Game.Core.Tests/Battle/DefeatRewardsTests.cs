using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.TestInfrastructure.Builders;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Battle
{
    public class DefeatRewardsTests
    {
        [Fact]
        public void ExpReward_EqualCoreAttributeTotals_MultiplierIsOne()
        {
            var player = MakePlayer(allocations: [(Strength, 10), (Endurance, 5)]);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // enemy total = 15, player core total = 15; ratio 1.0 ∈ [0.8, 1.2] → exp = floor(15 * 1) = 15
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_RatioNearOne_MultiplierIsOne()
        {
            var player = MakePlayer(allocations: [(Strength, 11), (Endurance, 5)]);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // ratio = 15/16 ≈ 0.9375 ∈ [0.8, 1.2] → multiplier 1.0 → exp = floor(15) = 15
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_WeakEnemy_ReducedExp()
        {
            var player = MakePlayer(allocations: [(Strength, 60), (Endurance, 40)]);
            var enemy = MakeEnemy(strength: 5, endurance: 5);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // ratio = 10/100 = 0.1 < 0.8 → multiplier 0.1^2 = 0.01 → exp = floor(10 * 0.01) = 0
            Assert.Equal(0, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_StrongEnemy_IncreasedExp()
        {
            var player = MakePlayer(allocations: [(Strength, 50)]);
            var enemy = MakeEnemy(strength: 50, endurance: 25);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // ratio = 75/50 = 1.5 > 1.2 → multiplier 1.5^2 = 2.25 (below the cap) → exp = floor(75 * 2.25) = 168
            Assert.Equal(168, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_FarOverLevelEnemy_MultiplierClampedAtMax()
        {
            var player = MakePlayer(allocations: [(Strength, 10)]);
            var enemy = MakeEnemy(strength: 50, endurance: 50);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // ratio = 100/10 = 10 → uncapped multiplier would be 100; clamped to MaxExpRewardMultiplier (4)
            // → exp = floor(100 * 4) = 400 instead of the unbounded floor(100 * 100) = 10000.
            Assert.Equal(4.0, ServerGameConstants.MaxExpRewardMultiplier);
            Assert.Equal(400, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_AtTwicePlayerPower_MultiplierExactlyAtCap()
        {
            var player = MakePlayer(allocations: [(Strength, 25)]);
            var enemy = MakeEnemy(strength: 30, endurance: 20);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // ratio = 50/25 = 2.0 → multiplier 2^2 = 4.0, exactly MaxExpRewardMultiplier (the saturation
            // boundary) → exp = floor(50 * 4) = 200.
            Assert.Equal(200, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_FarUnderLevelEnemy_QuadraticDropOffUnaffectedByCap()
        {
            var player = MakePlayer(allocations: [(Strength, 100)]);
            var enemy = MakeEnemy(strength: 10);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // ratio = 10/100 = 0.1 ≪ 0.8 → multiplier 0.1^2 = 0.01 (the cap only bounds the upper tail) →
            // exp = floor(10 * 0.01) = 0.
            Assert.Equal(0, rewards.ExpReward);
        }

        [Theory]
        // ratio ∈ [0.8, 1.2] → multiplier 1; below → ratio²; above → ratio² clamped at MaxExpRewardMultiplier.
        [InlineData(15, 15, 1.0)]    // ratio 1.0
        [InlineData(5, 100, 0.0025)] // ratio 0.05 → 0.0025 (trivial enemy → tiny multiplier, anti-grind)
        [InlineData(75, 50, 2.25)]   // ratio 1.5 → 2.25
        [InlineData(100, 10, 4.0)]   // ratio 10 → clamped at the cap (4)
        public void DifficultyMultiplier_FollowsTheSameCurveAsTheExpReward(int enemyStrength, int playerStrength, double expected)
        {
            var player = MakePlayer(allocations: [(Strength, playerStrength)]);
            var enemy = MakeEnemy(strength: enemyStrength);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // The proficiency-XP accrual scales its fixed pie by this same factor, so it is exposed for reuse
            // rather than recomputed.
            Assert.Equal(expected, rewards.DifficultyMultiplier, precision: 9);
        }

        [Fact]
        public void DifficultyMultiplier_ZeroPlayerAttributes_IsNeutral()
        {
            var player = MakePlayer(allocations: []);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // No player investment falls back to a neutral multiplier (the reward is then the floored enemy
            // total), matching the original exp guard.
            Assert.Equal(1.0, rewards.DifficultyMultiplier, precision: 9);
        }

        [Fact]
        public void ExpReward_ZeroPlayerAttributes_ReturnsFlooredEnemyTotal()
        {
            var player = MakePlayer(allocations: []);
            var enemy = MakeEnemy(strength: 10, endurance: 5);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // No player attribute investment → guard returns floor(enemy total) = floor(15) = 15
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_NewPlayer_UsesAllocatedAttributesNotStatPointsGainedCount()
        {
            // A new player has a starter spread allocated (sum 30) but StatPointsGained == 0 — the
            // starter spread is not counted as "gained". The denominator must be the allocated total.
            var player = MakePlayer(
                allocations: [(Strength, 5), (Endurance, 5), (Intellect, 5), (Agility, 5), (Dexterity, 5), (Luck, 5)],
                statPointsGained: 0);
            var enemy = MakeEnemy(strength: 15);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // ratio = 15/30 = 0.5 < 0.8 → multiplier 0.25 → exp = floor(15 * 0.25) = 3.
            // The old formula divided by StatPointsGained (0) and returned the full floor(15) = 15.
            Assert.Equal(3, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_DoesNotDependOnStatPointsGained()
        {
            // Two identical players that differ only in StatPointsGained must earn identical exp,
            // proving the reward no longer keys off the stat-point count.
            var enemy = MakeEnemy(strength: 30);
            var lowGained = MakePlayer(allocations: [(Strength, 15), (Endurance, 15)], statPointsGained: 0);
            var highGained = MakePlayer(allocations: [(Strength, 15), (Endurance, 15)], statPointsGained: 999);

            var lowReward = new DefeatRewards(lowGained.GetAllModifiers(), enemy);
            var highReward = new DefeatRewards(highGained.GetAllModifiers(), enemy);

            // ratio = 30/30 = 1.0 → exp = floor(30) = 30 for both
            Assert.Equal(30, lowReward.ExpReward);
            Assert.Equal(30, highReward.ExpReward);
        }

        [Fact]
        public void ExpReward_IncludesEquippedItemCoreAttributes()
        {
            var player = MakePlayer(allocations: [(Strength, 10)]);
            EquipAccessory(player, [Modifier(Strength, 5, EModifierType.Additive)]);
            var enemy = MakeEnemy(strength: 15);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // Player core total = 10 (allocation) + 5 (gear) = 15; ratio 1.0 → exp = floor(15) = 15
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_ExcludesDerivedAndMultiplicativeModifiers()
        {
            var player = MakePlayer(allocations: [(Strength, 15)]);
            EquipAccessory(player,
            [
                Modifier(MaxHealth, 100, EModifierType.Additive),   // derived attribute → excluded
                Modifier(Strength, 1.5, EModifierType.Multiplicative), // scaling factor → excluded
            ]);
            var enemy = MakeEnemy(strength: 15);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            // Only the additive core Strength (15) counts; ratio 15/15 = 1.0 → exp = floor(15) = 15.
            // Were the derived/multiplicative modifiers summed, the denominator would balloon and the
            // reward would collapse.
            Assert.Equal(15, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_EnemyTotalAboveIntRange_ClampsToIntMaxValueInsteadOfWrapping()
        {
            // No player investment hits the playerAttTotal <= 0 path, which returns the floored enemy total.
            // A huge authored enemy power (author-controlled level × per-level slope) can exceed int.MaxValue;
            // the unclamped (int) cast would wrap to a negative value that GrantExp floors to 0, silently
            // zeroing a legitimate reward. The reward must clamp to int.MaxValue instead.
            var player = MakePlayer(allocations: []);
            var enemy = MakeEnemy(strength: 3_000_000_000); // above int.MaxValue (≈ 2.147e9)

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            Assert.Equal(int.MaxValue, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_MultipliedProductAboveIntRange_ClampsToIntMaxValue()
        {
            // ratio = 1e9 / 1 ≫ 1.2 → multiplier clamps to MaxExpRewardMultiplier (4); the product
            // 1e9 × 4 = 4e9 still exceeds int.MaxValue, so the cap alone does not keep the cast in range.
            var player = MakePlayer(allocations: [(Strength, 1)]);
            var enemy = MakeEnemy(strength: 1_000_000_000);

            var rewards = new DefeatRewards(player.GetAllModifiers(), enemy);

            Assert.Equal(int.MaxValue, rewards.ExpReward);
        }

        private static Player MakePlayer(
            (EAttribute Attribute, double Amount)[] allocations,
            int statPointsGained = 0) =>
            new PlayerBuilder()
                .WithStatAllocations(allocations.Select(a => new StatAllocation { Attribute = a.Attribute, Amount = a.Amount }))
                .WithStatPointsGained(statPointsGained)
                .WithStatPointsUsed((int)allocations.Sum(a => a.Amount))
                .Build();

        private static void EquipAccessory(Player player, List<AttributeModifier> attributes)
        {
            var item = new Item
            {
                Id = 10,
                Name = "Accessory",
                Description = string.Empty,
                Category = EItemCategory.Accessory,
                Rarity = ERarity.Common,
                Attributes = attributes,
                ModSlots = [],
            };
            player.Inventory.UnlockItem(item);
            player.TryEquipItem(item.Id, EEquipmentSlot.AccessorySlot);
        }

        private static AttributeModifier Modifier(EAttribute attribute, double amount, EModifierType type) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = type,
            Source = EAttributeModifierSource.Item,
        };

        private static Enemy MakeEnemy(double strength = 0, double endurance = 0) => new()
        {
            Id = 1,
            Name = "Test Enemy",
            IsBoss = false,
            Level = 1,
            AttributeDistributions =
            [
                new AttributeDistribution
                {
                    AttributeId = Strength,
                    BaseAmount = (decimal)strength,
                    AmountPerLevel = 0,
                },
                new AttributeDistribution
                {
                    AttributeId = Endurance,
                    BaseAmount = (decimal)endurance,
                    AmountPerLevel = 0,
                },
            ],
            AvailableSkills = [],
        };
    }
}
