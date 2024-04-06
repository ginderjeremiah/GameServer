using GameLibrary;
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
            var endpointMetadata = new List<EndpointMetaData>();
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
                    var methodAtt = endpoint.GetCustomAttributes().FirstOrDefault(att => att is HttpMethodAttribute) as HttpMethodAttribute;
                    if (methodAtt is not null && methodAtt.Template is not null)
                    {
                        routeTemplate = methodAtt.Template;
                    }
                    if (endpoint.GetCustomAttribute<RouteAttribute>() is RouteAttribute routeAtt && routeAtt.Template is not null)
                    {
                        routeTemplate = routeAtt.Template;
                    }
                    var route = routeTemplate is not null
                        ? routeTemplate.Replace("[controller]", controllerName).Replace("[action]", endpoint.Name)
                        : $"/api/{controllerName}/{endpoint.Name}";

                    var responseType = endpoint.ReturnType.IsConstructedGenericType
                        ? endpoint.ReturnType.IsAssignableTo(typeof(IApiListResponse))
                            ? typeof(List<>).MakeGenericType(endpoint.ReturnType.GetGenericArguments()[0])
                            : endpoint.ReturnType.GetGenericArguments()[0]
                        : null;

                    if (responseType != null && NeedsInterface(responseType))
                    {
                        usedTypes.UnionWith(GetInterfaceTypes(responseType));
                    }

                    var metaData = new EndpointMetaData
                    {
                        Endpoint = route,
                        ResponseType = responseType,
                        Parameters = endpoint.GetParameters().Select(p => new EndpointParameterData
                        {
                            ParameterName = p.Name,
                            ParameterType = p.ParameterType,
                            HasDefault = p.HasDefaultValue,
                        }).ToList(),
                        IsGet = methodAtt.HttpMethods.Contains("GET")
                    };

                    endpointMetadata.Add(metaData);

                    usedTypes.UnionWith(metaData.Parameters.Where(p => NeedsInterface(p.ParameterType)).SelectMany(p => GetInterfaceTypes(p.ParameterType)));
                }
            }

            var currentDir = Directory.GetCurrentDirectory();
            var assemblyName = assembly.GetName().Name;
            var projectDir = currentDir[..(currentDir.LastIndexOf(assemblyName) + assemblyName.Length)];
            var targetDir = $"{projectDir}\\TypeScript\\Game\\Shared\\Api";
            var typeMapPath = $"{targetDir}\\ApiTypeMap.ts";
            var apiInterfacesBasePath = $"{targetDir}\\ApiInterfaces";
            File.Delete(typeMapPath);
            if (Directory.Exists(apiInterfacesBasePath))
                Directory.Delete(apiInterfacesBasePath, true);
            WriteResponseTypes(endpointMetadata.OrderBy(end => end.Endpoint).ToList(), typeMapPath);
            WriteApiInterfaces(usedTypes, apiInterfacesBasePath);
        }

        private static void WriteResponseTypes(List<EndpointMetaData> endpointData, string filePath)
        {
            var strBuilder = new StringBuilder();
            strBuilder.AppendLine("type ApiResponseTypes = {");
            foreach (var endpoint in endpointData)
            {
                strBuilder.AppendLine($"\t'{endpoint.Endpoint}': {GetTypeText(endpoint.ResponseType)}");
            }
            strBuilder.AppendLine("}\n");

            strBuilder.AppendLine("type ApiRequestTypes = {");
            foreach (var endpoint in endpointData)
            {
                strBuilder.AppendLine($"\t'{endpoint.Endpoint}': {GetParametersTypeText(endpoint)}");
            }
            strBuilder.AppendLine("}\n");

            strBuilder.AppendLine("type ApiEndpoint = keyof ApiResponseTypes | keyof ApiRequestTypes\n");
            strBuilder.AppendLine("type ApiResponseType = ApiResponseTypes[ApiEndpoint]\n");
            strBuilder.AppendLine("type ApiRequestType = ApiRequestTypes[ApiEndpoint]");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, strBuilder.ToString());
        }

        private static string GetParametersTypeText(EndpointMetaData endpoint)
        {
            if (endpoint.Parameters.Count == 0)
            {
                return "undefined";
            }
            if (endpoint.Parameters.Count == 1 && !endpoint.IsGet)
            {
                return endpoint.Parameters[0].TypeText;
            }
            else //Construct anonymous json object type
            {
                return $"{{ {string.Join(", ", endpoint.Parameters.Select(p => $"{p.ParameterName.ToCamelCase()}: {p.TypeText}"))} }}";
            }
        }

        private static string GetTypeText(Type? type)
        {
            if (type == null)
            {
                return "undefined";
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
                || type == typeof(uint?))
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
            else if (type.IsEnum)
            {
                return type.Name;
            }
            else if (IsListType(type))
            {
                return $"{GetTypeText(type.GetGenericArguments()[0])}[]";
            }
            else
            {
                return GetInterfaceName(type);
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
                var filePath = $"{baseDirectoryPath}\\{nameSpace[(nameSpace.LastIndexOf('.') + 1)..]}.ts";
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
            builder.AppendLine($"interface {GetInterfaceName(type)} {{");
            if (type.IsGenericTypeDefinition)
            {
                var generics = type.GetGenericArguments().Select((t, i) => (type: t, index: i));
                foreach (var prop in type.GetProperties())
                {
                    var generic = generics.FirstOrDefault(x => x.type == prop.PropertyType);
                    var typeText = generic == default
                        ? GetTypeText(prop.PropertyType)
                        : "T" + generic.index.ToString();
                    builder.AppendLine($"\t{prop.Name.ToCamelCase()}: {typeText};");
                }
            }
            else
            {
                foreach (var prop in type.GetProperties())
                {
                    builder.AppendLine($"\t{prop.Name.ToCamelCase()}: {GetTypeText(prop.PropertyType)};");
                }
            }
            builder.Append('}');
        }

        private static List<Type> GetSubTypes(Type type)
        {
            var props = type.GetProperties().Where(prop => NeedsInterface(prop.PropertyType));
            var subTypes = props.SelectMany(prop => GetInterfaceTypes(prop.PropertyType).SelectMany(GetSubTypes));
            return props.SelectMany(prop => GetInterfaceTypes(prop.PropertyType)).Concat(subTypes).ToList();
        }

        private static bool NeedsInterface(Type type)
        {
            if (IsListType(type))
            {
                return NeedsInterface(type.GetGenericArguments()[0]);
            }
            else
            {
                return type.IsClass && !type.IsGenericParameter && type != typeof(string);
            }
        }

        private static List<Type> GetInterfaceTypes(Type type)
        {
            if (IsListType(type))
            {
                return GetInterfaceTypes(type.GetGenericArguments()[0]);
            }
            else if (type.IsConstructedGenericType)
            {
                return type.GetGenericArguments()
                    .SelectMany(GetInterfaceTypes)
                    .Append(type.GetGenericTypeDefinition())
                    .ToList();
            }
            else if (type.IsClass && type != typeof(string))
            {
                return new List<Type> { type };
            }
            else
            {
                return new List<Type>();
            }
        }

        private static bool IsListType(Type type)
        {
            return type.IsGenericType && type.IsAssignableTo(typeof(System.Collections.IList));
        }

        private static string GetInterfaceName(Type type)
        {
            if (type.IsGenericTypeDefinition)
            {
                return $"I{type.Name[..type.Name.IndexOf("`")]}<{string.Join(", ", type.GetGenericArguments().Select((arg, i) => "T" + i))}>";
            }
            else if (type.IsConstructedGenericType)
            {
                return $"I{type.Name[..type.Name.IndexOf("`")]}<{string.Join(", ", type.GetGenericArguments().Select(GetTypeText))}>";
            }
            else
            {
                return $"I{type.Name}";
            }
        }

        private class EndpointMetaData
        {
            public string Endpoint { get; set; }
            public Type? ResponseType { get; set; }
            public List<EndpointParameterData> Parameters { get; set; }
            public bool IsGet { get; set; }
        }

        private class EndpointParameterData
        {
            public string ParameterName { get; set; }
            public Type ParameterType { get; set; }
            public bool HasDefault { get; set; }

            public string TypeText
            {
                get
                {
                    if (HasDefault)
                    {
                        return GetTypeText(ParameterType) + " | undefined";
                    }
                    else
                    {
                        return GetTypeText(ParameterType);
                    }
                }
            }
        }
    }
}
