using GameServer.Controllers;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using System.Text;

namespace GameServer.CodeGen
{
    public static class ApiCodeGenerator
    {
        public static void GenerateResponseInterfaces()
        {
            var usedTypes = new HashSet<Type>();
            var endpointResponseMap = new List<EndpointResponseData>();
            var assembly = Assembly.GetAssembly(typeof(ApiCodeGenerator));
            var controllerTypes = assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(BaseController)));
            foreach (var controller in controllerTypes)
            {
                var controllerRouteTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template;
                var controllerName = controller.Name.Replace("Controller", string.Empty);
                var endpointMethods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var endpoint in endpointMethods.Where(method => method.GetCustomAttribute<NonActionAttribute>() is null && method.ReturnType.IsAssignableTo(typeof(IApiResponse))))
                {
                    var routeTemplate = controllerRouteTemplate;
                    if (endpoint.GetCustomAttribute<RouteAttribute>() is RouteAttribute routeAtt && routeAtt.Template is not null)
                    {
                        routeTemplate = routeAtt.Template;
                    }
                    else if (endpoint.GetCustomAttributes().FirstOrDefault(att => att is HttpMethodAttribute) is HttpMethodAttribute methodAtt && methodAtt.Template is not null)
                    {
                        routeTemplate = methodAtt.Template;
                    }
                    var route = routeTemplate is not null
                        ? routeTemplate.Replace("[controller]", controllerName).Replace("[action]", endpoint.Name)
                        : $"/api/{controllerName}/{endpoint.Name}";

                    var responseType = endpoint.ReturnType.IsConstructedGenericType
                        ? endpoint.ReturnType.GetGenericArguments()[0]
                        : null;

                    if (responseType != null && NeedsInterface(responseType))
                    {
                        usedTypes.Add(GetInterfaceType(responseType));
                    }

                    endpointResponseMap.Add(new EndpointResponseData
                    {
                        Endpoint = route,
                        ResponseType = responseType,
                    });
                }
            }

            var currentDir = Directory.GetCurrentDirectory();
            var assemblyName = assembly.GetName().Name;
            var projectDir = currentDir[..(currentDir.LastIndexOf(assemblyName) + assemblyName.Length)];
            var targetDir = $"{projectDir}\\TypeScript\\Game\\Shared\\Api";
            var responseTypesPath = $"{targetDir}\\ApiResponseTypes.ts";
            var apiInterfacesBasePath = $"{targetDir}\\ApiInterfaces";
            File.Delete(responseTypesPath);
            if (Directory.Exists(apiInterfacesBasePath))
                Directory.Delete(apiInterfacesBasePath, true);
            WriteResponseTypes(endpointResponseMap.OrderBy(end => end.Endpoint).ToList(), responseTypesPath);
            WriteApiInterfaces(usedTypes, apiInterfacesBasePath);
        }

        private static void WriteResponseTypes(List<EndpointResponseData> endpointData, string filePath)
        {
            var strBuilder = new StringBuilder();
            strBuilder.AppendLine("type ApiResponseTypes = {");
            foreach (var endpoint in endpointData)
            {
                strBuilder.AppendLine($"\t'{endpoint.Endpoint}': {GetTypeText(endpoint.ResponseType)}");
            }
            strBuilder.Append('}');

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, strBuilder.ToString());
        }

        private static string GetTypeText(Type? type)
        {
            if (type == null)
            {
                return "void";
            }
            else if (type == typeof(int)
                || type == typeof(int?)
                || type == typeof(decimal)
                || type == typeof(decimal?)
                || type == typeof(double)
                || type == typeof(double?)
                || type == typeof(float)
                || type == typeof(float?)
                || type == typeof(short)
                || type == typeof(short?)
                || type == typeof(long)
                || type == typeof(long?)
                || type == typeof(uint)
                || type == typeof(uint?)
                || type.IsEnum)
            {
                return "number";
            }
            else if (type == typeof(bool) || type == typeof(bool?))
            {
                return "boolean";
            }
            else if (type == typeof(string))
            {
                return "string";
            }
            else if (IsListType(type))
            {
                return $"{GetTypeText(type.GetGenericArguments()[0])}[]";
            }
            else
            {
                return $"I{type.Name}";
            }
        }

        private static void WriteApiInterfaces(HashSet<Type> types, string baseDirectoryPath)
        {
            var fileBuilders = new Dictionary<string, StringBuilder>();
            var subTypes = types.Select(GetSubTypes).ToList();

            foreach (var subTypeList in subTypes)
            {
                types.UnionWith(subTypeList);
            }

            foreach (var type in types)
            {
                var nameSpace = type.Namespace;
                var filePath = $"{baseDirectoryPath}{nameSpace[nameSpace.IndexOf('.')..].Replace('.', '\\')}.ts";
                fileBuilders.TryGetValue(filePath, out var builder);
                if (builder == null)
                {
                    builder = new StringBuilder();
                    fileBuilders.Add(filePath, builder);
                }
                else
                {
                    builder.Append("\n\n");
                }
                WriteInterfaceToBuilder(type, builder);
            }

            foreach ((var path, var builder) in fileBuilders)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, builder.ToString());
            }
        }

        private static void WriteInterfaceToBuilder(Type type, StringBuilder builder)
        {
            builder.AppendLine($"interface I{type.Name} {{");
            foreach (var prop in type.GetProperties())
            {
                var propName = string.Concat(prop.Name[0].ToString().ToLower(), prop.Name.AsSpan(1));
                builder.AppendLine($"\t{propName}: {GetTypeText(prop.PropertyType)};");
            }
            builder.Append('}');
        }

        private static List<Type> GetSubTypes(Type type)
        {
            var props = type.GetProperties().Where(prop => NeedsInterface(prop.PropertyType));
            var subTypes = props.SelectMany(prop => GetSubTypes(GetInterfaceType(prop.PropertyType)));
            return props.Select(prop => GetInterfaceType(prop.PropertyType)).Concat(subTypes).ToList();
        }

        private static bool NeedsInterface(Type type)
        {
            if (IsListType(type))
            {
                return NeedsInterface(type.GetGenericArguments()[0]);
            }
            else
            {
                return type.IsClass && type != typeof(string);
            }
        }

        private static Type GetInterfaceType(Type type)
        {
            if (IsListType(type))
            {
                return GetInterfaceType(type.GetGenericArguments()[0]);
            }
            else if (type.IsClass && type != typeof(string))
            {
                return type;
            }
            else
            {
                return null;
            }
        }

        private static bool IsListType(Type type)
        {
            return type.IsGenericType && type.IsAssignableTo(typeof(System.Collections.IList));
        }

        private class EndpointResponseData
        {
            public string Endpoint { get; set; }
            public Type? ResponseType { get; set; }
        }
    }
}
