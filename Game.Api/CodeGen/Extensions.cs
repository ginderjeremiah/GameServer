using System.Reflection;

namespace Game.Api.CodeGen
{
    internal static class CodeGenExtensions
    {
        internal static NullabilityInfo GetNullabilityInfo(this ParameterInfo parameter)
        {
            var nullabilityContext = new NullabilityInfoContext();
            return nullabilityContext.Create(parameter);
        }

        internal static NullabilityInfo GetNullabilityInfo(this PropertyInfo parameter)
        {
            var nullabilityContext = new NullabilityInfoContext();
            return nullabilityContext.Create(parameter);
        }

        /// <summary>
        /// Walks <paramref name="type"/>'s base-class chain for the closed constructed type that
        /// matches the supplied open generic base definition (e.g. <c>AbstractSocketCommand&lt;,&gt;</c>),
        /// or <c>null</c> if the type does not derive from it. Used by the codegen to resolve a command's
        /// response/parameter members against the typed generic base instead of by raw member name.
        /// </summary>
        internal static Type? GetClosedGenericBase(this Type type, Type openGenericBase)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase)
                {
                    return current;
                }
            }

            return null;
        }

        internal static bool NeedsInterface(this Type type)
        {
            if (type.IsEnumerable())
            {
                if (type.IsDictionary())
                {
                    return type.GetGenericArguments()[1].NeedsInterface();
                }

                return type.GetGenericArguments()[0].NeedsInterface();
            }
            else
            {
                return type.IsEnum || IsClassThatNeedsInterface(type) || IsStructThatNeedsInterface(type);
            }
        }

        internal static bool IsDictionary(this Type type)
        {
            if (type.IsGenericType && type.GenericTypeArguments.Length == 2)
            {
                var genericDef = type.GetGenericTypeDefinition();
                return genericDef == typeof(Dictionary<,>)
                    || genericDef == typeof(IDictionary<,>)
                    || genericDef == typeof(IReadOnlyDictionary<,>);
            }

            return false;
        }

        internal static bool IsEnumerable(this Type type)
        {
            if (type.IsGenericType)
            {
                if (type.IsAssignableTo(typeof(System.Collections.IEnumerable)))
                {
                    return true;
                }
                else if (type.GenericTypeArguments.Length == 1)
                {
                    var asyncEnumType = typeof(IAsyncEnumerable<>).MakeGenericType(type.GetGenericArguments());
                    return type.IsAssignableTo(asyncEnumType);
                }
            }

            return false;
        }

        private static bool IsClassThatNeedsInterface(Type type)
        {
            return type.IsClass && !type.IsGenericParameter && type != typeof(string);
        }

        // Structs intended to be mirrored to TypeScript as interfaces. This is an explicit allow-list,
        // not a deny-list: an unlisted struct (Guid, TimeSpan, DateTimeOffset, DateOnly, …) has no
        // TypeScript mapping, so it must fall through to GetTypeText's throw and surface at generation
        // time rather than silently emit a reference to an interface that is never generated. A
        // deny-list would let any not-yet-excluded BCL struct slip through that safety net. DateTime and
        // decimal are mapped to primitives directly in GetTypeText, so they never reach here.
        private static readonly HashSet<Type> MirroredStructs = [];

        private static bool IsStructThatNeedsInterface(Type type)
        {
            return type.IsValueType && MirroredStructs.Contains(type);
        }
    }
}
