using Game.Api.CodeGen.Data;
using Game.Core;

namespace Game.Api.CodeGen
{
    internal class CodeGenTypeFormatter
    {
        public string? GetImportText(CodeGenTypeDescriptor descriptor)
        {
            if (descriptor.UnderlyingType.IsEnumerable())
            {
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

        public string GetTypeText(CodeGenTypeDescriptor? descriptor)
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
            else if (type == typeof(int?)
                || type == typeof(decimal?)
                || type == typeof(double?)
                || type == typeof(float?)
                || type == typeof(short?)
                || type == typeof(long?)
                || type == typeof(uint?))
            {
                return "number";
            }
            else if (type == typeof(bool))
            {
                return "boolean";
            }
            else if (type == typeof(bool?))
            {
                return "boolean";
            }
            else if (type == typeof(string))
            {
                return "string";
            }
            else if (type.IsEnum)
            {
                return type.Name;
            }
            else if (type.IsEnumerable())
            {
                var genericParameter = descriptor.GenericArgumentDescriptors[0];
                var typeText = genericParameter.IsNullable ? $"({GetTypeText(genericParameter)} | undefined)" : GetTypeText(genericParameter);
                return $"{typeText}[]";
            }
            else
            {
                return GetInterfaceName(descriptor);
            }
        }

        public string GetParametersTypeText(EndpointMetadata endpoint)
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

        public string GetParameterText(CodeGenTypeDescriptor descriptor, bool useGenericParameters = false)
        {
            useGenericParameters &= descriptor.GenericParameterPosition >= 0;
            var parameterName = useGenericParameters ? ((char)('T' + descriptor.GenericParameterPosition)).ToString() : GetTypeText(descriptor);
            return $"{descriptor.Name?.Decapitalize()}{(descriptor.HasDefault || descriptor.IsNullable ? "?" : "")}: {parameterName}";
        }

        public string GetInterfaceName(CodeGenTypeDescriptor descriptor, bool useGenericParameters = false)
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
