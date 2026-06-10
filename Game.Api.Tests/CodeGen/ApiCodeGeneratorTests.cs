using Game.Api.CodeGen;
using Game.Core;
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
            var fieldNames = ApiCodeGenerator.GetClientMirroredConstantFields()
                .Select(field => field.Name)
                .ToList();

            Assert.Equal(fieldNames.OrderBy(name => name), fieldNames);
        }
    }
}
