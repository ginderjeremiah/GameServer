using Game.Core;
using System.Reflection;

namespace Game.Api.CodeGen
{
    public class CodeGenTypeDescriptor
    {
        public string? Name { get; }
        public Type UnderlyingType { get; }
        public bool HasDefault { get; } = false;
        public bool IsNullable { get; }
        public List<CodeGenTypeDescriptor> GenericArgumentDescriptors { get; }
        public List<CodeGenTypeDescriptor> PropertyDescriptors { get; }
        public int GenericParameterPosition { get; } = -1;

        public bool NeedsInterface => UnderlyingType.NeedsInterface();
        public string TypeName => IsGeneric ? UnderlyingType.Name[..UnderlyingType.Name.IndexOf('`')] : UnderlyingType.Name;
        public string LastNamespacePart => UnderlyingType.Namespace is null ? "Unknown" : UnderlyingType.Namespace[(UnderlyingType.Namespace.LastIndexOf('.') + 1)..].SnakeCase();
        public bool IsGeneric => GenericArgumentDescriptors.Count > 0;
        public bool IsEnum => UnderlyingType.IsEnum;

        public CodeGenTypeDescriptor(PropertyInfo property, int genericParameterPosition = -1) : this(property.GetNullabilityInfo())
        {
            Name = property.Name;
            GenericParameterPosition = genericParameterPosition;
        }

        public CodeGenTypeDescriptor(ParameterInfo parameter) : this(parameter.GetNullabilityInfo())
        {
            Name = parameter.Name;
            HasDefault = parameter.HasDefaultValue;
        }

        public CodeGenTypeDescriptor(NullabilityInfo nullabilityInfo, Type? overrideType = null)
        {
            UnderlyingType = overrideType ?? GetUnderlyingType(nullabilityInfo);
            IsNullable = nullabilityInfo.ReadState == NullabilityState.Nullable;
            GenericArgumentDescriptors = nullabilityInfo.GenericTypeArguments.Select(g => new CodeGenTypeDescriptor(g)).ToList();
            if (UnderlyingType.IsGenericType)
            {
                var genericDefinition = UnderlyingType.GetGenericTypeDefinition();
                var propertyPairs = UnderlyingType.GetProperties().Zip(genericDefinition.GetProperties());
                PropertyDescriptors = propertyPairs.Select(p => new CodeGenTypeDescriptor(p.First, p.Second.PropertyType.IsGenericParameter ? p.Second.PropertyType.GenericParameterPosition : -1)).ToList();
            }
            else if (UnderlyingType.NeedsInterface())
            {
                PropertyDescriptors = UnderlyingType.GetProperties().Select(p => new CodeGenTypeDescriptor(p)).ToList();
            }
            else
            {
                PropertyDescriptors = [];
            }
        }

        private static Type GetUnderlyingType(NullabilityInfo nullabilityInfo)
        {
            if (nullabilityInfo.Type.IsGenericType && nullabilityInfo.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return nullabilityInfo.Type.GenericTypeArguments[0];
            }

            return nullabilityInfo.Type;
        }
    }
}
