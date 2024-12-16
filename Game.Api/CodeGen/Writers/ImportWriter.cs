using Game.Core;

namespace Game.Api.CodeGen.Writers
{
    internal class ImportWriter
    {
        public string ImportPath { get; }

        public ImportWriter(string importPath = "./")
        {
            ImportPath = importPath;
        }

        public string GetImportText(IEnumerable<CodeGenTypeDescriptor> typeDescriptors)
        {
            var formatter = new CodeGenTypeFormatter();
            var typeStrings = typeDescriptors.SelectNotNull(formatter.GetImportText).Distinct().OrderBy(t => t);
            return typeStrings.Count() > 3
                ? $"import type {{\n\t{string.Join(",\n\t", typeStrings)}\n}} from \"{ImportPath}\"\n"
                : $"import type {{ {string.Join(", ", typeStrings)} }} from \"{ImportPath}\"\n";
        }
    }
}
