using Game.Core;
using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The rarity → proficiency-XP tier weight curve (#1123): a geometric multiplier on a contributing skill's
    /// attention, Common at the <c>1</c> baseline and rising with rarity. These pin the baseline, strict
    /// monotonicity (every tier weighs more than the one below — the pull a rarer skill exerts on its path's
    /// pie), the all-positive floor (a low-rarity skill still earns, never walled), and the geometric shape.
    /// </summary>
    public class ProficiencyTierWeightTests
    {
        [Fact]
        public void For_Common_IsTheUnitBaseline()
        {
            Assert.Equal(1.0, ProficiencyTierWeight.For(ERarity.Common), precision: 9);
        }

        [Fact]
        public void For_RisesStrictlyAndStaysPositiveAcrossEveryRarity()
        {
            var rarities = new[]
            {
                ERarity.Common, ERarity.Uncommon, ERarity.Rare,
                ERarity.Epic, ERarity.Legendary, ERarity.Mythic,
            };

            var previous = 0.0;
            foreach (var rarity in rarities)
            {
                var weight = ProficiencyTierWeight.For(rarity);
                // Positive: even the lowest tier carries attention, so it trains (slowly), never walled.
                Assert.True(weight > 0, $"{rarity} weight should be positive");
                // Strictly increasing: a rarer skill out-pulls a more common one for the same pie.
                Assert.True(weight > previous, $"{rarity} weight should exceed the previous tier's");
                previous = weight;
            }
        }

        [Theory]
        [InlineData(ERarity.Common, 1.0)]
        [InlineData(ERarity.Uncommon, 1.5)]
        [InlineData(ERarity.Rare, 2.25)]
        [InlineData(ERarity.Epic, 3.375)]
        [InlineData(ERarity.Legendary, 5.0625)]
        [InlineData(ERarity.Mythic, 7.59375)]
        public void For_IsGeometricInTheRarityRank(ERarity rarity, double expected)
        {
            Assert.Equal(expected, ProficiencyTierWeight.For(rarity), precision: 9);
        }
    }
}
