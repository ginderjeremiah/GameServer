using Game.Api.CodeGen.Data;
using Game.Core;

namespace Game.Api.CodeGen
{
    internal static class CodeGenTypeFormatter
    {
        public static string GetImportText(IEnumerable<CodeGenTypeDescriptor> typeDescriptors, string importPath = "./")
        {
            var typeStrings = typeDescriptors.SelectMany(GetImportTexts).Distinct().OrderBy(t => t).ToList();
            return typeStrings.Count > 3
                ? $"import type {{{Environment.NewLine}\t{string.Join($",{Environment.NewLine}\t", typeStrings)}{Environment.NewLine}}} from '{importPath}';{Environment.NewLine}"
                : $"import type {{ {string.Join(", ", typeStrings)} }} from '{importPath}';{Environment.NewLine}";
        }

        /// <summary>
        /// Every interface/enum import a descriptor's rendered TypeScript type requires, walking through
        /// enumerable elements and <b>both</b> dictionary type arguments. A <c>Record&lt;K, V&gt;</c> names
        /// both K and V, so an enum (or otherwise importable) key needs its import collected just like the
        /// value — collecting only the value would emit an unimported reference and break the frontend build.
        /// </summary>
        public static IEnumerable<string> GetImportTexts(CodeGenTypeDescriptor descriptor)
        {
            if (descriptor.UnderlyingType.IsEnumerable())
            {
                if (descriptor.UnderlyingType.IsDictionary())
                {
                    return GetImportTexts(descriptor.GenericArgumentDescriptors[0])
                        .Concat(GetImportTexts(descriptor.GenericArgumentDescriptors[1]));
                }

                return GetImportTexts(descriptor.GenericArgumentDescriptors[0]);
            }

            var leaf = GetLeafImportText(descriptor);
            return leaf is null ? [] : [leaf];
        }

        /// <summary>
        /// A single descriptor's own import name, used as a stable identity key by the interface writer's
        /// de-duplication. Unlike <see cref="GetImportTexts"/> this collapses a dictionary to its value
        /// import only, so it must not be used to collect the full import set (see <see cref="GetImportTexts"/>).
        /// </summary>
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

            return GetLeafImportText(descriptor);
        }

        // The import name for a single non-enumerable descriptor: its enum name, its I-prefixed interface
        // name, or null when it maps to a primitive that needs no import.
        private static string? GetLeafImportText(CodeGenTypeDescriptor descriptor)
        {
            if (descriptor.IsEnum)
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
