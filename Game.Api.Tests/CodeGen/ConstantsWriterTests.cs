using Game.Api.CodeGen;
using Game.Api.CodeGen.Writers;
using Game.Core.TestInfrastructure.Helpers;
using System.Reflection;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class ConstantsWriterTests : IDisposable
    {
        private const string Comment = "// Auto-generated\n\n";
        private readonly CodeGenOptions _options;

        public ConstantsWriterTests()
        {
            _options = new CodeGenOptions
            {
                TargetDirectory = Path.Combine(Path.GetTempPath(), "codegen_constants_test_" + Guid.NewGuid().ToString("N")),
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

        // A fixture of constants the writer reflects over, covering the value kinds it renders.
        private static class SampleConstants
        {
            public const int MsPerTick = 40;
            public const int DefaultMaxBattleMs = MsPerTick * 10000;
            public const bool FlagEnabled = true;
            public const string Label = "hello";
            public const string Tricky = "a'b\\c";
            public const int NegativeOffset = -5;
            public const double FractionalRate = 1.5;
        }

        private static FieldInfo[] SampleFields =>
            typeof(SampleConstants).GetFields(BindingFlags.Public | BindingFlags.Static);

        [Fact]
        public void WriteConstants_WritesFile_StartingWithComment()
        {
            var content = WriteAndRead(SampleFields);

            Assert.StartsWith(Comment, content);
        }

        [Fact]
        public void WriteConstants_RendersNumericConstantsAsScreamingSnakeCaseExports()
        {
            var content = WriteAndRead(SampleFields);

            Assert.Contains("export const MS_PER_TICK = 40;", content);
            Assert.Contains("export const DEFAULT_MAX_BATTLE_MS = 400000;", content);
        }

        [Fact]
        public void WriteConstants_RendersBooleanAndStringConstants()
        {
            var content = WriteAndRead(SampleFields);

            Assert.Contains("export const FLAG_ENABLED = true;", content);
            Assert.Contains("export const LABEL = 'hello';", content);
        }

        [Fact]
        public void WriteConstants_EscapesQuotesAndBackslashesInStrings()
        {
            var content = WriteAndRead(SampleFields);

            // 'a'b\c' would be a broken (or injected) TS literal; the quote and backslash must escape.
            Assert.Contains(@"export const TRICKY = 'a\'b\\c';", content);
        }

        [Theory]
        [InlineData("MsPerTick", "MS_PER_TICK")]
        [InlineData("DefaultMaxBattleMs", "DEFAULT_MAX_BATTLE_MS")]
        [InlineData("MaxSelectedSkills", "MAX_SELECTED_SKILLS")]
        [InlineData("ExpPerLevel", "EXP_PER_LEVEL")]
        [InlineData("StatPointsPerLevel", "STAT_POINTS_PER_LEVEL")]
        public void ToScreamingSnakeCase_ConvertsPascalCaseNames(string pascalCase, string expected)
        {
            Assert.Equal(expected, ConstantsWriter.ToScreamingSnakeCase(pascalCase));
        }

        [Fact]
        public void WriteConstants_SwedishCulture_RendersNumbersInvariantly()
        {
            // The numeric sibling of the tr-TR casing guards below. sv-SE writes the decimal separator as
            // "," and the negative sign as U+2212 MINUS SIGN, so a current-culture render would emit
            // "= 1,5" (a TS syntax change) and a non-ASCII minus into the byte-compared game-constants.ts.
            using var culture = new CultureScope("sv-SE");

            var content = WriteAndRead(SampleFields);

            Assert.Contains("export const FRACTIONAL_RATE = 1.5;", content, StringComparison.Ordinal);
            Assert.Contains("export const NEGATIVE_OFFSET = -5;", content, StringComparison.Ordinal);
        }

        [Fact]
        public void ToScreamingSnakeCase_TurkishCulture_UppercasesInvariantly()
        {
            // The sibling of the Decapitalize/SnakeCase locale guards: a culture-sensitive ToUpper would
            // emit "İTEM_İD" here on a tr-TR machine, so the byte-compared game-constants.ts would depend
            // on the generating machine's locale.
            using var culture = new CultureScope("tr-TR");

            Assert.Equal("ITEM_ID", ConstantsWriter.ToScreamingSnakeCase("ItemId"));
        }

        private string WriteAndRead(IEnumerable<FieldInfo> constants)
        {
            new ConstantsWriter(_options).WriteConstants(constants, Comment);
            return File.ReadAllText(Path.Combine(_options.TargetDirectory, "game-constants.ts"));
        }
    }
}
