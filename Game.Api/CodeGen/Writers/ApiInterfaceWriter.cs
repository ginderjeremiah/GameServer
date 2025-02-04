using Game.Api.CodeGen.Data;
using Game.Core;
using System.Text;

namespace Game.Api.CodeGen.Writers
{
    internal class ApiInterfaceWriter : FileWriter
    {
        public string TargetDir { get; set; }

        public ApiInterfaceWriter(string targetDir)
        {
            TargetDir = targetDir;
        }

        public void WriteApiInterfaces(IEnumerable<CodeGenTypeDescriptor> descriptors)
        {
            var interfacesFolder = "interfaces";
            var enumPath = "enums.ts";
            var exportPath = "index.ts";

            var interfacesExist = Directory.Exists($"{TargetDir}\\{interfacesFolder}");
            var existingFiles = interfacesExist ? Directory.GetFiles($"{TargetDir}\\{interfacesFolder}").ToList() : [];
            if (!interfacesExist)
            {
                Directory.CreateDirectory($"{TargetDir}\\{interfacesFolder}");
            }

            var allDescriptors = descriptors
                .SelectMany(GetAllUsedDescriptors)
                .Where(d => d.NeedsInterface)
                .DistinctBy(CodeGenTypeFormatter.GetImportText);

            var interfaceDataGroups = allDescriptors.Select(d => new InterfaceDescriptorData
            {
                Descriptor = d,
                FilePath = d.IsEnum ? enumPath : $"{interfacesFolder}\\{d.LastNamespacePart}.ts"
            })
            .GroupBy(data => data.FilePath);

            foreach (var group in interfaceDataGroups)
            {
                var fileBuilder = new StringBuilder();
                var currentExports = group.SelectNotNull(d => CodeGenTypeFormatter.GetImportText(d.Descriptor));
                var importedDescriptors = group
                    .SelectMany(data => data.Descriptor.PropertyDescriptors)
                    .Where(d => d.GenericParameterPosition < 0 && d.NeedsInterface)
                    .ExceptBy(currentExports, CodeGenTypeFormatter.GetImportText);

                if (importedDescriptors.Any())
                {
                    fileBuilder.AppendLine(CodeGenTypeFormatter.GetImportText(importedDescriptors, "../"));
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

                OverwriteFileIfTextDiffers($"{TargetDir}\\{group.Key}", fileBuilder.ToString().TrimEnd());
                existingFiles.Remove($"{TargetDir}\\{group.Key}");
            }

            OverwriteFileIfTextDiffers($"{TargetDir}\\{exportPath}", string.Join("\n", interfaceDataGroups.Select(g => $"export * from \"./{g.Key.Replace("\\", "/")}\"")));

            foreach (var file in existingFiles)
            {
                File.Delete(file);
            }
        }

        private static void WriteEnumToBuilder(CodeGenTypeDescriptor descriptor, StringBuilder builder)
        {
            ;
            builder.AppendLine($"export enum {CodeGenTypeFormatter.GetTypeText(descriptor)} {{");
            var values = descriptor.UnderlyingType.GetEnumValues();
            foreach (var value in values)
            {
                builder.AppendLine($"\t{value.ToString()} = {(int)value},");
            }

            builder.Append("};");
        }

        private static void WriteInterfaceToBuilder(CodeGenTypeDescriptor descriptor, StringBuilder builder)
        {
            builder.AppendLine($"export interface {CodeGenTypeFormatter.GetInterfaceName(descriptor, true)} {{");
            foreach (var prop in descriptor.PropertyDescriptors)
            {
                builder.AppendLine($"\t{CodeGenTypeFormatter.GetParameterText(prop, true)};");
            }

            builder.Append("};");
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
