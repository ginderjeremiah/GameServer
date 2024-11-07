using Game.Api.CodeGen.Data;
using Game.Core;
using System.Text;

namespace Game.Api.CodeGen.Writers
{
    internal class ApiInterfaceWriter
    {
        public string TargetDir { get; set; }

        public ApiInterfaceWriter(string targetDir)
        {
            TargetDir = targetDir;
        }

        public void WriteApiInterfaces(IEnumerable<CodeGenTypeDescriptor> descriptors)
        {
            var interfacesPath = "interfaces";
            var enumPath = "enums.ts";
            var exportPath = "index.ts";

            if (Directory.Exists($"{TargetDir}\\{interfacesPath}"))
                Directory.Delete($"{TargetDir}\\{interfacesPath}", true);

            Directory.CreateDirectory($"{TargetDir}\\{interfacesPath}");

            var formatter = new CodeGenTypeFormatter();

            var allDescriptors = descriptors
                .SelectMany(GetAllUsedDescriptors)
                .Where(d => d.NeedsInterface)
                .DistinctBy(formatter.GetImportText);

            var interfaceDataGroups = allDescriptors.Select(d => new InterfaceDescriptorData
            {
                Descriptor = d,
                FilePath = d.IsEnum ? enumPath : $"{interfacesPath}\\{d.LastNamespacePart}.ts"
            })
            .GroupBy(data => data.FilePath);

            foreach (var group in interfaceDataGroups)
            {
                var fileBuilder = new StringBuilder();
                var currentExports = group.SelectNotNull(d => formatter.GetImportText(d.Descriptor));
                var importedDescriptors = group
                    .SelectMany(data => data.Descriptor.PropertyDescriptors)
                    .Where(d => d.GenericParameterPosition < 0)
                    .ExceptBy(currentExports, formatter.GetImportText);

                var importWriter = new ImportWriter("../");

                if (importedDescriptors.Any())
                {
                    fileBuilder.AppendLine(importWriter.GetImportText(importedDescriptors));
                }

                foreach (var interfaceType in group)
                {
                    if (interfaceType.Descriptor.IsEnum)
                    {
                        WriteEnumToBuilder(interfaceType.Descriptor, fileBuilder);
                    }
                    else
                    {
                        WriteInterfaceToBuilder(interfaceType.Descriptor, fileBuilder);
                    }

                    fileBuilder.AppendLine("\n");
                }

                File.WriteAllText($"{TargetDir}\\{group.Key}", fileBuilder.ToString().TrimEnd());
            }

            File.WriteAllText($"{TargetDir}\\{exportPath}", string.Join("\n", interfaceDataGroups.Select(g => $"export * from \"./{g.Key.Replace("\\", "/")}\"")));
        }

        private static void WriteEnumToBuilder(CodeGenTypeDescriptor descriptor, StringBuilder builder)
        {
            var formatter = new CodeGenTypeFormatter();
            builder.AppendLine($"export enum {formatter.GetTypeText(descriptor)} {{");
            var values = descriptor.UnderlyingType.GetEnumValues();
            foreach (var value in values)
            {
                builder.AppendLine($"\t{value.ToString()} = {(int)value},");
            }

            builder.Append('}');
        }

        private static void WriteInterfaceToBuilder(CodeGenTypeDescriptor descriptor, StringBuilder builder)
        {
            var formatter = new CodeGenTypeFormatter();
            builder.AppendLine($"export interface {formatter.GetInterfaceName(descriptor, true)} {{");
            foreach (var prop in descriptor.PropertyDescriptors)
            {
                builder.AppendLine($"\t{formatter.GetParameterText(prop, true)};");
            }

            builder.Append('}');
        }

        private static IEnumerable<CodeGenTypeDescriptor> GetAllUsedDescriptors(CodeGenTypeDescriptor type)
        {
            var childTypes = type.PropertyDescriptors
                .Concat(type.GenericArgumentDescriptors);

            return childTypes
                .SelectMany(GetAllUsedDescriptors)
                .Append(type);
        }
    }
}
