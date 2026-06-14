using Game.Api.CodeGen.Data;
using Game.Core;

namespace Game.Api.CodeGen
{
    internal static class CodeGenTypeFormatter
    {
        public static string GetImportText(IEnumerable<CodeGenTypeDescriptor> typeDescriptors, string importPath = "./")
        {
            var typeStrings = typeDescriptors.SelectNotNull(GetImportText).Distinct().OrderBy(t => t).ToList();
            return typeStrings.Count > 3
                ? $"import type {{{Environment.NewLine}\t{string.Join($",{Environment.NewLine}\t", typeStrings)}{Environment.NewLine}}} from '{importPath}';{Environment.NewLine}"
                : $"import type {{ {string.Join(", ", typeStrings)} }} from '{importPath}';{Environment.NewLine}";
        }

        public static string? GetImportText(CodeGenTypeDescriptor descriptor)
        {
            if (descriptor.UnderlyingType.IsEnumerable())
            {
                if (descriptor.UnderlyingType.IsDictionary())
                {
                    return GetImportText(descriptor.GenericArgumentDescriptors[1]);
                }

                return GetImportText(descriptor.GenericArgumentDescriptors[0]);
            }
            else if (descriptor.IsEnum)
            {
                return descriptor.TypeName;
            }
            else if (descriptor.NeedsInterface)
            {
                return $"I{descriptor.TypeName}";
            }

            return null;
        }

        public static string GetTypeText(CodeGenTypeDescriptor? descriptor)
        {
            if (descriptor is null)
            {
                return "undefined";
            }

            var type = descriptor.UnderlyingType;
            if (type == typeof(int)
                || type == typeof(decimal)
                || type == typeof(double)
                || type == typeof(float)
                || type == typeof(short)
                || type == typeof(long)
                || type == typeof(uint))
            {
                return "number";
            }
            else if (type == typeof(bool))
            {
                return "boolean";
            }
            else if (type == typeof(string) || type == typeof(DateTime))
            {
                return "string";
            }
            else if (type.IsEnum)
            {
                return type.Name;
            }
            else if (type.IsEnumerable())
            {
                if (type.IsDictionary())
                {
                    var keyParameter = descriptor.GenericArgumentDescriptors[0];
                    var valueParameter = descriptor.GenericArgumentDescriptors[1];
                    var keyText = GetTypeText(keyParameter);
                    var valueText = valueParameter.IsNullable ? $"{GetTypeText(valueParameter)} | undefined" : GetTypeText(valueParameter);
                    return $"Record<{keyText}, {valueText}>";
                }

                var genericParameter = descriptor.GenericArgumentDescriptors[0];
                var typeText = genericParameter.IsNullable ? $"({GetTypeText(genericParameter)} | undefined)" : GetTypeText(genericParameter);
                return $"{typeText}[]";
            }
            else if (type.NeedsInterface())
            {
                return GetInterfaceName(descriptor);
            }
            else
            {
                // A type with no TypeScript mapping that NeedsInterface() also rejects (e.g. Guid,
                // TimeSpan, byte, char) would otherwise silently emit a reference to an interface that
                // is never generated, breaking the frontend build with nothing flagged here. Throw so
                // the unmapped type surfaces at generation time instead.
                throw new InvalidOperationException(
                    $"CodeGen has no TypeScript mapping for type '{type.FullName ?? type.Name}'. " +
                    "Add an explicit mapping in CodeGenTypeFormatter.GetTypeText (and CodeGenExtensions.NeedsInterface if it should generate an interface).");
            }
        }

        public static string GetParametersTypeText(EndpointMetadata endpoint)
        {
            if (endpoint.ParameterDescriptors.Count == 0)
            {
                return "void";
            }
            else if (endpoint.ParameterDescriptors.Count == 1 && endpoint.ParameterDescriptors[0].UnderlyingType.IsClass && endpoint.ParameterDescriptors[0].UnderlyingType != typeof(string))
            {
                var desc = endpoint.ParameterDescriptors[0];
                return desc.HasDefault || desc.IsNullable ? $"{GetTypeText(desc)} | undefined" : GetTypeText(desc);
            }
            else //Construct anonymous json object type
            {
                var text = $"{{ {string.Join(", ", endpoint.ParameterDescriptors.Select(d => GetParameterText(d)))} }}";
                return endpoint.ParameterDescriptors.All(d => d.IsNullable || d.HasDefault) ? $"{text} | undefined" : text;
            }
        }

        public static string GetParameterText(CodeGenTypeDescriptor descriptor, bool useGenericParameters = false)
        {
            useGenericParameters &= descriptor.GenericParameterPosition >= 0;
            var parameterName = useGenericParameters ? ((char)('T' + descriptor.GenericParameterPosition)).ToString() : GetTypeText(descriptor);
            return $"{descriptor.Name?.Decapitalize()}{(descriptor.HasDefault || descriptor.IsNullable ? "?" : "")}: {parameterName}";
        }

        public static string GetInterfaceName(CodeGenTypeDescriptor descriptor, bool useGenericParameters = false)
        {
            if (descriptor.GenericArgumentDescriptors.Count > 0)
            {
                var genericParameterNames = descriptor.GenericArgumentDescriptors.Select((d, i) => useGenericParameters ? ((char)('T' + i)).ToString() : GetTypeText(d));
                return $"I{descriptor.TypeName}<{string.Join(", ", genericParameterNames)}>";
            }
            else
            {
                return $"I{descriptor.TypeName}";
            }
        }
    }
}
