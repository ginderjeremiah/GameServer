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
                return type.GetGenericArguments()[0].NeedsInterface();
            }
            else
            {
                return type.IsEnum || type.IsClass && !type.IsGenericParameter && type != typeof(string);
            }
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
    }
}
