using Game.Core.Attributes.Modifiers;
using Game.Core.Players;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class StatAllocationTests
    {
        [Fact]
        public void ToModifier_ProducesAdditivePlayerStatPointsModifier()
        {
            var allocation = new StatAllocation { Attribute = EAttribute.Strength, Amount = 5 };

            var modifier = allocation.ToModifier();

            Assert.Equal(EAttribute.Strength, modifier.Attribute);
            Assert.Equal(5, modifier.Amount);
            Assert.Equal(EModifierType.Additive, modifier.Type);
            Assert.Equal(EAttributeModifierSource.PlayerStatPoints, modifier.Source);
        }

        [Fact]
        public void ToModifier_MatchesPlayerStatPointsToAttributeModifiers()
        {
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength, Amount = 4 },
                new() { Attribute = EAttribute.Agility, Amount = 6 },
            };
            var statPoints = new PlayerStatPoints(allocations) { StatPointsGained = 10, StatPointsUsed = 10 };

            var fromAllocations = allocations.Select(a => a.ToModifier()).ToList();
            var fromStatPoints = statPoints.ToAttributeModifiers().ToList();

            Assert.Equal(fromStatPoints.Count, fromAllocations.Count);
            for (var i = 0; i < fromStatPoints.Count; i++)
            {
                Assert.Equal(fromStatPoints[i].Attribute, fromAllocations[i].Attribute);
                Assert.Equal(fromStatPoints[i].Amount, fromAllocations[i].Amount);
                Assert.Equal(fromStatPoints[i].Type, fromAllocations[i].Type);
                Assert.Equal(fromStatPoints[i].Source, fromAllocations[i].Source);
            }
        }
    }
}
