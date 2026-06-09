using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    /// <summary>
    /// Locks down the level-scaling formula on <see cref="AttributeDistribution.GetDistributionModifier"/>:
    /// an enemy's level-scaled attribute is <c>BaseAmount + (AmountPerLevel * level)</c>, wrapped in an
    /// additive <see cref="AttributeModifier"/> sourced from <see cref="EAttributeModifierSource.AttributeDistribution"/>.
    /// </summary>
    public class AttributeDistributionTests
    {
        [Fact]
        public void GetDistributionModifier_AtLevelZero_UsesBaseAmount()
        {
            var distribution = new AttributeDistribution
            {
                AttributeId = EAttribute.Strength,
                BaseAmount = 10m,
                AmountPerLevel = 3m,
            };

            var modifier = distribution.GetDistributionModifier(0);

            Assert.Equal(10, modifier.Amount);
        }

        [Fact]
        public void GetDistributionModifier_ScalesLinearlyWithLevel()
        {
            var distribution = new AttributeDistribution
            {
                AttributeId = EAttribute.Endurance,
                BaseAmount = 10m,
                AmountPerLevel = 3m,
            };

            var modifier = distribution.GetDistributionModifier(5);

            // BaseAmount(10) + AmountPerLevel(3) * level(5) = 25.
            Assert.Equal(25, modifier.Amount);
        }

        [Fact]
        public void GetDistributionModifier_ProducesAdditiveModifierForTheDistributedAttribute()
        {
            var distribution = new AttributeDistribution
            {
                AttributeId = EAttribute.Agility,
                BaseAmount = 4m,
                AmountPerLevel = 1m,
            };

            var modifier = distribution.GetDistributionModifier(2);

            Assert.Equal(EAttribute.Agility, modifier.Attribute);
            Assert.Equal(EModifierType.Additive, modifier.Type);
            Assert.Equal(EAttributeModifierSource.AttributeDistribution, modifier.Source);
        }
    }
}
