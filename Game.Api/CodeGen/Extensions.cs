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

        private static bool IsStructThatNeedsInterface(Type type)
        {
            return type.IsValueType && !type.IsPrimitive && type != typeof(DateTime) && type != typeof(decimal);
        }
    }
}
