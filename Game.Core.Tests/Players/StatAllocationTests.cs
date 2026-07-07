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
    }
}
