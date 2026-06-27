using Game.Api.CodeGen;
using Game.Api.CodeGen.Writers;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class DamageTypeMapWriterTests : IDisposable
    {
        private const string Comment = "// Auto-generated\n\n";
        private readonly CodeGenOptions _options;

        public DamageTypeMapWriterTests()
        {
            _options = new CodeGenOptions
            {
                TargetDirectory = Path.Combine(Path.GetTempPath(), "codegen_damage_test_" + Guid.NewGuid().ToString("N")),
                NewLine = "\n"
            };
            Directory.CreateDirectory(_options.TargetDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_options.TargetDirectory))
            {
                Directory.Delete(_options.TargetDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteDamageTypeMaps_WritesFile_WithCommentImportAndConstExports()
        {
            var content = Write();

            Assert.StartsWith(Comment, content);
            Assert.Contains("import { EAttribute, EDamageType, EDamageTypeKey } from './enums.ts';", content);
            Assert.Contains("export const DAMAGE_TYPE_APPLIES = {", content);
            Assert.Contains("export const DAMAGE_TYPE_KEY_ATTRIBUTES = {", content);
            Assert.EndsWith("} as const;", content);
        }

        [Fact]
        public void WriteDamageTypeMaps_EmitsAppliesEntries_InFixedOrder()
        {
            var content = Write();

            Assert.Contains("[EDamageType.Physical]: [EDamageTypeKey.Physical],", content);
            // Burn pulls its own key plus Fire / Elemental / DoT, in that fixed order.
            Assert.Contains(
                "[EDamageType.Burn]: [EDamageTypeKey.Burn, EDamageTypeKey.Fire, EDamageTypeKey.Elemental, EDamageTypeKey.Dot],",
                content);
        }

        [Fact]
        public void WriteDamageTypeMaps_EmitsKeyAttributePairs()
        {
            var content = Write();

            Assert.Contains(
                "[EDamageTypeKey.Fire]: { amplification: EAttribute.FireAmplification, resistance: EAttribute.FireResistance },",
                content);
            Assert.Contains(
                "[EDamageTypeKey.Dot]: { amplification: EAttribute.DotAmplification, resistance: EAttribute.DotResistance },",
                content);
        }

        private string Write()
        {
            new DamageTypeMapWriter(_options).WriteDamageTypeMaps(Comment);
            return File.ReadAllText(Path.Combine(_options.TargetDirectory, "damage-types.ts"));
        }
    }
}
