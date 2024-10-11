using System.Reflection;

namespace GameServer.CodeGen
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
            if (IsListType(type))
            {
                return NeedsInterface(type.GetGenericArguments()[0]);
            }
            else
            {
                return type.IsEnum || (type.IsClass && !type.IsGenericParameter && type != typeof(string));
            }
        }

        internal static bool IsListType(this Type type)
        {
            return type.IsGenericType && type.IsAssignableTo(typeof(System.Collections.IList));
        }
    }
}
