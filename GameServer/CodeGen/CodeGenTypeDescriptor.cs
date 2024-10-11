using GameCore;
using System.Reflection;

namespace GameServer.CodeGen
{
    public class CodeGenTypeDescriptor
    {
        public string? Name { get; set; }
        public Type UnderlyingType { get; set; }
        public bool HasDefault { get; set; } = false;
        public bool IsNullable { get; set; }
        public List<CodeGenTypeDescriptor> GenericArgumentDescriptors { get; set; }
        public List<CodeGenTypeDescriptor> PropertyDescriptors { get; set; }
        public int GenericParameterPosition { get; set; } = -1;

        public bool NeedsInterface => UnderlyingType.NeedsInterface();
        public string TypeName => IsGeneric ? UnderlyingType.Name[..UnderlyingType.Name.IndexOf('`')] : UnderlyingType.Name;
        public string LastNamespacePart => UnderlyingType.Namespace[(UnderlyingType.Namespace.LastIndexOf('.') + 1)..].SnakeCase();
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
            UnderlyingType = overrideType ?? nullabilityInfo.Type;
            IsNullable = nullabilityInfo.ReadState == NullabilityState.Nullable;
            GenericArgumentDescriptors = nullabilityInfo.GenericTypeArguments.Select(g => new CodeGenTypeDescriptor(g)).ToList();
            if (UnderlyingType.IsGenericType)
            {
                var genericDefinition = UnderlyingType.GetGenericTypeDefinition();
                var propertyPairs = UnderlyingType.GetProperties().Zip(genericDefinition.GetProperties());
                PropertyDescriptors = propertyPairs.Select(p => new CodeGenTypeDescriptor(p.First, p.Second.PropertyType.IsGenericParameter ? p.Second.PropertyType.GenericParameterPosition : -1)).ToList();
            }
            else
            {
                PropertyDescriptors = UnderlyingType.GetProperties().Select(p => new CodeGenTypeDescriptor(p)).ToList();
            }
        }
    }
}
