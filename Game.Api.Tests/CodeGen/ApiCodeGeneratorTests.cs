using Game.Api.CodeGen;
using Game.Core;
using Game.Core.TestInfrastructure.Helpers;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class ApiCodeGeneratorTests
    {
        [Theory]
        [InlineData(nameof(EEquipmentSlot))]
        [InlineData(nameof(EModifierType))]
        [InlineData(nameof(EAttributeModifierSource))]
        public void GetClientMirroredEnumDescriptors_IncludesEnumsMarkedClientMirrored(string enumName)
        {
            var emittedNames = ApiCodeGenerator.GetClientMirroredEnumDescriptors()
                .Select(d => d.TypeName)
                .ToList();

            Assert.Contains(enumName, emittedNames);
        }

        [Theory]
        [InlineData(nameof(ERole))]      // hand-maintained string-keyed exception, deliberately not mirrored
        [InlineData(nameof(EAttribute))] // reached via the wire walk, not opted in here
        public void GetClientMirroredEnumDescriptors_ExcludesUnmarkedEnums(string enumName)
        {
            var emittedNames = ApiCodeGenerator.GetClientMirroredEnumDescriptors()
                .Select(d => d.TypeName)
                .ToList();

            Assert.DoesNotContain(enumName, emittedNames);
        }

        [Fact]
        public void GetClientMirroredEnumDescriptors_DescribesEnumTypes()
        {
            Assert.All(
                ApiCodeGenerator.GetClientMirroredEnumDescriptors(),
                descriptor => Assert.True(descriptor.IsEnum));
        }

        [Theory]
        [InlineData(nameof(GameConstants.MsPerTick))]
        [InlineData(nameof(GameConstants.DefaultMaxBattleMs))]
        [InlineData(nameof(GameConstants.MaxSelectedSkills))]
        [InlineData(nameof(GameConstants.ExpPerLevel))]
        [InlineData(nameof(GameConstants.StatPointsPerLevel))]
        public void GetClientMirroredConstantFields_IncludesGameConstants(string fieldName)
        {
            var fieldNames = ApiCodeGenerator.GetClientMirroredConstantFields()
                .Select(field => field.Name)
                .ToList();

            Assert.Contains(fieldName, fieldNames);
        }

        [Theory]
        [InlineData(nameof(ContentFieldLengths.EnemyNameMaxLength))]
        [InlineData(nameof(ContentFieldLengths.SkillDesignerNotesMaxLength))]
        public void GetClientMirroredConstantFields_IncludesContentFieldLengths(string fieldName)
        {
            var fieldNames = ApiCodeGenerator.GetClientMirroredConstantFields()
                .Select(field => field.Name)
                .ToList();

            Assert.Contains(fieldName, fieldNames);
        }

        [Theory]
        [InlineData(nameof(ServerGameConstants.MaxExpRewardMultiplier))] // server-authoritative reward clamp
        [InlineData(nameof(ServerGameConstants.MaxExpPerGrant))]         // server-only anti-cheat backstop
        public void GetClientMirroredConstantFields_ExcludesServerOnlyConstants(string fieldName)
        {
            // ServerGameConstants carries no [ClientMirrored], so its values must never reach the client.
            var fieldNames = ApiCodeGenerator.GetClientMirroredConstantFields()
                .Select(field => field.Name)
                .ToList();

            Assert.DoesNotContain(fieldName, fieldNames);
        }

        [Fact]
        public void GetClientMirroredConstantFields_OnlyReturnsCompileTimeConstants()
        {
            Assert.All(
                ApiCodeGenerator.GetClientMirroredConstantFields(),
                field => Assert.True(field.IsLiteral));
        }

        [Fact]
        public void GetClientMirroredConstantFields_IsOrderedDeterministically()
        {
            // Ordering is grouped by declaring type then field name (not a flat alphabetical sort by
            // name alone), so this holds regardless of how many [ClientMirrored] constant classes exist.
            // It is ordinal, not culture-aware: the emitted file is byte-compared by CI, so the order
            // must not shift with the generating machine's locale or ICU version.
            var actual = ApiCodeGenerator.GetClientMirroredConstantFields()
                .Select(field => (TypeName: field.DeclaringType?.Name, field.Name))
                .ToList();

            var expected = actual
                .OrderBy(entry => entry.TypeName, StringComparer.Ordinal)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetClientMirroredConstantFields_OrderIsUnaffectedByHostCulture()
        {
            // Insurance for future names rather than a pin on current behaviour: this reflects the real
            // Game.Core assembly, and no [ClientMirrored] name today collates differently between the two
            // cultures, so it passes either way. GetClientMirroredConstantFields_IsOrderedDeterministically
            // above is the case that actually states the ordinal contract.
            var invariantOrder = ApiCodeGenerator.GetClientMirroredConstantFields().Select(field => field.Name).ToList();

            using var culture = new CultureScope("tr-TR");
            var turkishOrder = ApiCodeGenerator.GetClientMirroredConstantFields().Select(field => field.Name).ToList();

            Assert.Equal(invariantOrder, turkishOrder);
        }
    }
}
