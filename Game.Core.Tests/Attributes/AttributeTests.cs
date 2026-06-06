using Game.Core;
using Xunit;
using Attribute = Game.Core.Attributes.Attribute;

namespace Game.Core.Tests.Attributes
{
    public class AttributeTests
    {
        [Theory]
        [InlineData(EAttribute.Strength, "Strength")]
        [InlineData(EAttribute.MaxHealth, "Max Health")]
        [InlineData(EAttribute.CooldownRecovery, "Cooldown Recovery")]
        [InlineData(EAttribute.CriticalChance, "Critical Chance")]
        public void Constructor_SetsIdAndHumanReadableName(EAttribute id, string expectedName)
        {
            var attribute = new Attribute(id);

            Assert.Equal(id, attribute.Id);
            Assert.Equal(expectedName, attribute.Name);
        }

        [Theory]
        [MemberData(nameof(AllAttributes))]
        public void Constructor_AssignsNonEmptyDescription(EAttribute id)
        {
            var attribute = new Attribute(id);

            Assert.False(string.IsNullOrWhiteSpace(attribute.Description));
        }

        [Fact]
        public void Description_IsDistinctForEachAttribute()
        {
            var descriptions = Attribute.GetAllAttributes().Select(a => a.Description).ToList();

            Assert.Equal(descriptions.Count, descriptions.Distinct().Count());
        }

        [Fact]
        public void GetAllAttributes_ReturnsOneEntryPerAttribute()
        {
            var all = Attribute.GetAllAttributes().ToList();

            var expectedIds = Enum.GetValues<EAttribute>();
            Assert.Equal(expectedIds.Length, all.Count);
            Assert.Equal(expectedIds, all.Select(a => a.Id).ToArray());
        }

        public static IEnumerable<object[]> AllAttributes()
        {
            return Enum.GetValues<EAttribute>().Select(a => new object[] { a });
        }
    }
}
