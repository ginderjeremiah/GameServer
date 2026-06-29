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

        [Theory]
        [MemberData(nameof(AllAttributes))]
        public void Constructor_PopulatesDisplayMetadataForEveryAttribute(EAttribute id)
        {
            var attribute = new Attribute(id);

            Assert.True(Enum.IsDefined(attribute.AttributeType));
            Assert.NotNull(attribute.Code);
            Assert.True(attribute.DisplayOrder >= 0);
            Assert.True(attribute.Decimals >= 0);
        }

        [Fact]
        public void PrimaryAttributeType_MatchesCoreAttributes()
        {
            var primary = Attribute.GetAllAttributes()
                .Where(a => a.AttributeType == EAttributeType.Primary)
                .Select(a => a.Id);

            // The Primary display taxonomy is expected to coincide with the core/derived power-calc
            // invariant, but the two are deliberately distinct concepts (this asserts the coincidence).
            Assert.Equal(Attribute.CoreAttributes.OrderBy(a => a), primary.OrderBy(a => a));
        }

        [Fact]
        public void DisplayOrder_IsDistinctAcrossAttributes()
        {
            var orders = Attribute.GetAllAttributes().Select(a => a.DisplayOrder).ToList();

            Assert.Equal(orders.Count, orders.Distinct().Count());
        }

        [Theory]
        [InlineData(EAttribute.Strength, EAttributeType.Primary, "STR")]
        [InlineData(EAttribute.Luck, EAttributeType.Primary, "LUK")]
        [InlineData(EAttribute.MaxHealth, EAttributeType.Secondary, "HP")]
        [InlineData(EAttribute.Toughness, EAttributeType.Secondary, "TGH")]
        [InlineData(EAttribute.CooldownRecovery, EAttributeType.Secondary, "CDR")]
        [InlineData(EAttribute.BleedDamagePerSecond, EAttributeType.Status, "BLD DOT")]
        [InlineData(EAttribute.PoisonDamagePerSecond, EAttributeType.Status, "PSN DOT")]
        [InlineData(EAttribute.BurnDamagePerSecond, EAttributeType.Status, "BRN DOT")]
        [InlineData(EAttribute.HealthRegenPerSecond, EAttributeType.Status, "REG")]
        [InlineData(EAttribute.FireAmplification, EAttributeType.Affinity, "FIR AMP")]
        [InlineData(EAttribute.ElementalResistance, EAttributeType.Affinity, "ELE RES")]
        [InlineData(EAttribute.DotResistance, EAttributeType.Affinity, "DOT RES")]
        public void Constructor_AssignsTypeAndCode(EAttribute id, EAttributeType expectedType, string expectedCode)
        {
            var attribute = new Attribute(id);

            Assert.Equal(expectedType, attribute.AttributeType);
            Assert.Equal(expectedCode, attribute.Code);
        }

        [Theory]
        [InlineData(EAttribute.FireAmplification, EDamageTypeKey.Fire)]
        [InlineData(EAttribute.FireResistance, EDamageTypeKey.Fire)]
        [InlineData(EAttribute.DotAmplification, EDamageTypeKey.Dot)]
        public void Constructor_TagsAmpResistAttributesWithTheirDamageTypeKey(EAttribute id, EDamageTypeKey expectedKey)
        {
            var attribute = new Attribute(id);

            Assert.Equal(expectedKey, attribute.DamageTypeKey);
            Assert.True(attribute.IsPercentage);
        }

        [Theory]
        [InlineData(EAttribute.Strength)]
        [InlineData(EAttribute.Toughness)]
        public void Constructor_LeavesDamageTypeKeyNullForNonAmpResistAttributes(EAttribute id)
        {
            Assert.Null(new Attribute(id).DamageTypeKey);
        }

        [Theory]
        [InlineData(EAttribute.BleedDamagePerSecond, true)]
        [InlineData(EAttribute.PoisonDamagePerSecond, true)]
        [InlineData(EAttribute.BurnDamagePerSecond, true)]
        [InlineData(EAttribute.HealthRegenPerSecond, false)]
        [InlineData(EAttribute.Strength, false)]
        [InlineData(EAttribute.MaxHealth, false)]
        public void Constructor_FlagsHarmfulAttributes(EAttribute id, bool expectedHarmful)
        {
            Assert.Equal(expectedHarmful, new Attribute(id).IsHarmful);
        }

        [Theory]
        [InlineData(EAttribute.CooldownRecovery, true, 0)]
        [InlineData(EAttribute.CriticalChance, true, 0)]
        [InlineData(EAttribute.DodgeChance, true, 0)]
        [InlineData(EAttribute.DamageReflection, true, 0)]
        [InlineData(EAttribute.Strength, false, 0)]
        [InlineData(EAttribute.MaxHealth, false, 0)]
        public void Constructor_AssignsPercentageAndDecimals(EAttribute id, bool expectedPercentage, int expectedDecimals)
        {
            var attribute = new Attribute(id);

            Assert.Equal(expectedPercentage, attribute.IsPercentage);
            Assert.Equal(expectedDecimals, attribute.Decimals);
        }

        public static IEnumerable<object[]> AllAttributes()
        {
            return Enum.GetValues<EAttribute>().Select(a => new object[] { a });
        }
    }
}
