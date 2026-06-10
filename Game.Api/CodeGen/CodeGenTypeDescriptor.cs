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
        public bool IsObsolete { get; } = false;
        public string? ObsoleteMessage { get; }

        public bool NeedsInterface => UnderlyingType.NeedsInterface();
        public string TypeName => IsGeneric ? UnderlyingType.Name[..UnderlyingType.Name.IndexOf('`')] : UnderlyingType.Name;
        public string LastNamespacePart => UnderlyingType.Namespace is null ? "Unknown" : UnderlyingType.Namespace[(UnderlyingType.Namespace.LastIndexOf('.') + 1)..].SnakeCase();
        public bool IsGeneric => GenericArgumentDescriptors.Count > 0;
        public bool IsEnum => UnderlyingType.IsEnum;

        public CodeGenTypeDescriptor(PropertyInfo property, int genericParameterPosition = -1) : this(property.GetNullabilityInfo())
        {
            Name = property.Name;
            GenericParameterPosition = genericParameterPosition;
            var obsoleteAttribute = property.GetCustomAttribute<ObsoleteAttribute>();
            if (obsoleteAttribute is not null)
            {
                IsObsolete = true;
                ObsoleteMessage = obsoleteAttribute.Message;
            }
        }

        public CodeGenTypeDescriptor(ParameterInfo parameter) : this(parameter.GetNullabilityInfo())
        {
            Name = parameter.Name;
            HasDefault = parameter.HasDefaultValue;
            var obsoleteAttribute = parameter.GetCustomAttribute<ObsoleteAttribute>();
            if (obsoleteAttribute is not null)
            {
                IsObsolete = true;
                ObsoleteMessage = obsoleteAttribute.Message;
            }
        }

        /// <summary>
        /// Builds a descriptor for a standalone enum type the reflection walk never reaches through a
        /// DTO member (a <see cref="ClientMirroredAttribute"/> domain enum). Enums are non-generic,
        /// non-nullable value types, so the nullability/generic machinery the member-based constructors
        /// derive from <see cref="NullabilityInfo"/> does not apply and is left at its default.
        /// </summary>
        public CodeGenTypeDescriptor(Type enumType)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException($"Only enum types are supported by this constructor; '{enumType.Name}' is not an enum.", nameof(enumType));
            }

            UnderlyingType = enumType;
            Name = enumType.Name;
            GenericArgumentDescriptors = [];
            PropertyDescriptors = [];

            var obsoleteAttribute = enumType.GetCustomAttribute<ObsoleteAttribute>();
            if (obsoleteAttribute is not null)
            {
                IsObsolete = true;
                ObsoleteMessage = obsoleteAttribute.Message;
            }
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
                PropertyDescriptors = propertyPairs.Select(p =>
                    new CodeGenTypeDescriptor(p.First, p.Second.PropertyType.IsGenericParameter ? p.Second.PropertyType.GenericParameterPosition : -1)
                ).ToList();
            }
            else if (UnderlyingType.NeedsInterface())
            {
                PropertyDescriptors = UnderlyingType.GetProperties().Select(p => new CodeGenTypeDescriptor(p)).ToList();
            }
            else
            {
                PropertyDescriptors = [];
            }

            var obsoleteAttribute = UnderlyingType.GetCustomAttribute<ObsoleteAttribute>();
            if (obsoleteAttribute is not null)
            {
                IsObsolete = true;
                ObsoleteMessage = obsoleteAttribute.Message;
            }
        }

        public IEnumerable<CodeGenTypeDescriptor> GetDirectlyReferencedDescriptorsForProperties()
        {
            foreach (var property in PropertyDescriptors)
            {
                yield return property;
                foreach (var reference in property.GetGenericArgumentReferences())
                {
                    yield return reference;
                }
            }
        }

        /// <summary>
        /// This descriptor together with every type reachable through its generic arguments
        /// (recursively). A rendered type's text references all of these — e.g. <c>IChange<IItem>[]</c>
        /// names both <c>IChange</c> and <c>IItem</c> — so the map writers use this to collect the full
        /// import set rather than only the outermost type.
        /// </summary>
        public IEnumerable<CodeGenTypeDescriptor> GetSelfAndGenericArgumentReferences()
        {
            yield return this;
            foreach (var reference in GetGenericArgumentReferences())
            {
                yield return reference;
            }
        }

        private IEnumerable<CodeGenTypeDescriptor> GetGenericArgumentReferences()
        {
            foreach (var genericArg in GenericArgumentDescriptors)
            {
                yield return genericArg;
                foreach (var reference in genericArg.GetGenericArgumentReferences())
                {
                    yield return reference;
                }
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
