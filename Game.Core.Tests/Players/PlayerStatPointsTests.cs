using Game.Core.Players;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class PlayerStatPointsTests
    {
        [Fact]
        public void TryUpdateAttributes_SpendingPoints_IncrementsStatPointsUsedByAmountSpent()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.True(result);
            Assert.Equal(3, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_SpendAllAvailable_UsesAllPoints()
        {
            var stats = MakeStats(gained: 6, used: 0);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 6)]);

            Assert.True(result);
            Assert.Equal(6, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_SpendMoreThanAvailable_ReturnsFalse()
        {
            var stats = MakeStats(gained: 5, used: 3);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.False(result);
            Assert.Equal(3, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_MultipleAllocations_SumsCorrectly()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([
                new Update(EAttribute.Strength, 2),
                new Update(EAttribute.Endurance, 3),
            ]);

            Assert.True(result);
            Assert.Equal(5, stats.StatPointsUsed);
            Assert.Equal(2, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(3, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void TryUpdateAttributes_NegativeAllocationWouldGoBelow_Zero_ReturnsFalse()
        {
            var stats = MakeStats(gained: 10, used: 5);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 2;

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, -3)]);

            Assert.False(result);
        }

        [Fact]
        public void TryUpdateAttributes_ZeroAmount_Succeeds()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 0)]);

            Assert.True(result);
            Assert.Equal(0, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_PartiallyUsedPoints_OnlyNewSpendAdded()
        {
            var stats = MakeStats(gained: 10, used: 4);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.True(result);
            Assert.Equal(7, stats.StatPointsUsed);
        }

        private static PlayerStatPoints MakeStats(int gained, int used)
        {
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,  Amount = 0 },
                new() { Attribute = EAttribute.Endurance, Amount = 0 },
                new() { Attribute = EAttribute.Intellect, Amount = 0 },
                new() { Attribute = EAttribute.Agility,   Amount = 0 },
                new() { Attribute = EAttribute.Dexterity, Amount = 0 },
                new() { Attribute = EAttribute.Luck,      Amount = 0 },
            };
            return new PlayerStatPoints(allocations)
            {
                StatPointsGained = gained,
                StatPointsUsed = used,
            };
        }

        private record Update(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
