using Game.Api.CodeGen;
using Game.Api.CodeGen.Writers;
using Game.Core;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class StaticModifierWriterTests : IDisposable
    {
        private const string Comment = "// Auto-generated\n\n";
        private readonly CodeGenOptions _options;

        public StaticModifierWriterTests()
        {
            _options = new CodeGenOptions
            {
                TargetDirectory = Path.Combine(Path.GetTempPath(), "codegen_static_test_" + Guid.NewGuid().ToString("N")),
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
        public void WriteStaticModifiers_WritesFile_WithCommentImportAndConstExport()
        {
            var content = WriteAndRead(StaticAttributeModifiers.All);

            Assert.StartsWith(Comment, content);
            Assert.Contains("import { EAttribute, EAttributeModifierSource, EModifierType } from './enums.ts';", content);
            Assert.Contains("export const STATIC_ATTRIBUTE_MODIFIERS = [", content);
            Assert.EndsWith("] as const;", content);
        }

        [Fact]
        public void WriteStaticModifiers_DerivedModifier_IncludesDerivedSource()
        {
            var modifier = new AttributeModifier
            {
                Attribute = EAttribute.MaxHealth,
                Amount = 20.0,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.Derived,
                DerivedSource = EAttribute.Endurance
            };

            var content = WriteAndRead([modifier]);

            Assert.Contains(
                "{ attribute: EAttribute.MaxHealth, amount: 20, type: EModifierType.Additive, source: EAttributeModifierSource.Derived, derivedSource: EAttribute.Endurance }",
                content);
        }

        [Fact]
        public void WriteStaticModifiers_NonDerivedModifier_OmitsDerivedSource()
        {
            // DerivedSource defaults to Strength on a non-derived modifier; it must not leak into the
            // generated object, or the frontend's discriminated BaseAttributeModifier (which carries no
            // derivedSource at all) would no longer match the generated table.
            var modifier = new AttributeModifier
            {
                Attribute = EAttribute.MaxHealth,
                Amount = 50.0,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.BaseValue
            };

            var content = WriteAndRead([modifier]);

            Assert.Contains(
                "{ attribute: EAttribute.MaxHealth, amount: 50, type: EModifierType.Additive, source: EAttributeModifierSource.BaseValue }",
                content);
            Assert.DoesNotContain("derivedSource", content);
        }

        [Fact]
        public void WriteStaticModifiers_PreservesModifierOrder()
        {
            var first = new AttributeModifier
            {
                Attribute = EAttribute.Strength,
                Amount = 1.0,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.BaseValue
            };
            var second = new AttributeModifier
            {
                Attribute = EAttribute.Agility,
                Amount = 2.0,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.BaseValue
            };

            var content = WriteAndRead([first, second]);

            Assert.True(content.IndexOf("EAttribute.Strength") < content.IndexOf("EAttribute.Agility"));
        }

        private string WriteAndRead(IReadOnlyList<AttributeModifier> modifiers)
        {
            new StaticModifierWriter(_options).WriteStaticModifiers(modifiers, Comment);
            return File.ReadAllText(Path.Combine(_options.TargetDirectory, "attribute-modifiers.ts"));
        }
    }
}
