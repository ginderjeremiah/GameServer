using GameCore;
using GameServer.Controllers;
using GameServer.Models.Common;
using GameServer.Sockets.Commands;
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
            var socketMetaData = new List<SocketCommandMetadata>();
            var endpointMetadata = new List<EndpointMetaData>();
            var assembly = typeof(ApiCodeGenerator).Assembly;
            var controllerTypes = assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(BaseController)));
            foreach (var controller in controllerTypes)
            {
                var controllerRouteTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template;
                var controllerName = controller.Name.Replace("Controller", string.Empty);
                var endpointMethods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var endpoints = endpointMethods
                    .Where(method => method.GetCustomAttribute<NonActionAttribute>() is null
                        && (method.ReturnType.IsAssignableTo(typeof(IApiResponse))
                            || (method.ReturnType.IsAssignableTo(typeof(Task))
                                && method.ReturnType.IsGenericType
                                && method.ReturnType.GetGenericArguments()[0].IsAssignableTo(typeof(IApiResponse))))
                     );

                foreach (var endpoint in endpoints)
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

                    var unwrappedResponseType = endpoint.ReturnType.IsAssignableTo(typeof(Task))
                        ? endpoint.ReturnType.GetGenericArguments()[0] : endpoint.ReturnType;

                    var responseType = unwrappedResponseType.IsConstructedGenericType
                        ? unwrappedResponseType.IsAssignableTo(typeof(IApiListResponse))
                            ? typeof(List<>).MakeGenericType(unwrappedResponseType.GetGenericArguments()[0])
                            : unwrappedResponseType.GetGenericArguments()[0]
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
                        IsGet = methodAtt?.HttpMethods?.Contains("GET") ?? true
                    };

                    endpointMetadata.Add(metaData);

                    usedTypes.UnionWith(metaData.Parameters.Where(p => NeedsInterface(p.ParameterType)).SelectMany(p => GetInterfaceTypes(p.ParameterType)));
                }
            }

            var socketCommandTypes = assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(AbstractSocketCommand)) && !type.IsAbstract);
            foreach (var socketCommand in socketCommandTypes)
            {
                Type? parameterType = null;
                Type? responseType = null;
                if (socketCommand.BaseType is Type baseType && baseType.IsGenericType)
                {
                    parameterType = baseType.GetGenericArguments()[0];
                }

                var execute = socketCommand.GetMethod("ExecuteInternal", BindingFlags.NonPublic | BindingFlags.Instance) ?? socketCommand.GetMethod("Execute");
                if (execute is not null)
                {
                    var unwrappedResponseType = execute.ReturnType.IsAssignableTo(typeof(Task))
                        ? execute.ReturnType.GetGenericArguments()[0] : execute.ReturnType;

                    responseType = unwrappedResponseType.IsConstructedGenericType
                        ? unwrappedResponseType.GetGenericArguments()[0] : null;
                }

                if (responseType != null && NeedsInterface(responseType))
                {
                    usedTypes.UnionWith(GetInterfaceTypes(responseType));
                }

                if (parameterType != null && NeedsInterface(parameterType))
                {
                    usedTypes.UnionWith(GetInterfaceTypes(parameterType));
                }

                socketMetaData.Add(new SocketCommandMetadata
                {
                    CommandName = socketCommand.Name,
                    ParameterType = parameterType,
                    ResponseType = responseType,
                });
            }

            var currentDir = Directory.GetCurrentDirectory();
            var assemblyName = assembly.GetName().Name ?? "Unknown";
            var projectDir = currentDir[..(currentDir.LastIndexOf(assemblyName) + assemblyName.Length)];
            var targetDir = $"{projectDir}\\TypeScript\\Game\\Shared\\Api";
            WriteApiMap([.. endpointMetadata.OrderBy(end => end.Endpoint)], targetDir);
            WriteApiSocketMap([.. socketMetaData.OrderBy(c => c.CommandName)], targetDir);
            WriteApiInterfaces(usedTypes, targetDir);
        }

        private static void WriteApiMap(List<EndpointMetaData> endpointData, string baseDirectoryPath)
        {
            var filePath = $"{baseDirectoryPath}\\ApiTypeMap.ts";
            var strBuilder = new StringBuilder();
            strBuilder.AppendLine("type ApiResponseTypes = {");
            foreach (var endpoint in endpointData)
            {
                strBuilder.AppendLine($"\t'{endpoint.Endpoint}': {GetTypeText(endpoint.ResponseType)}");
            }

            strBuilder.AppendLine("}\n");

            strBuilder.AppendLine("type ApiRequestTypes = {");
            foreach (var endpoint in endpointData.Where(endp => endp.Parameters.Count > 0))
            {
                strBuilder.AppendLine($"\t'{endpoint.Endpoint}': {GetParametersTypeText(endpoint)}");
            }

            strBuilder.AppendLine("}\n");
            strBuilder.AppendLine("type ApiEndpoint = keyof ApiResponseTypes\n");
            strBuilder.AppendLine("type ApiEndpointWithRequest = keyof ApiRequestTypes\n");
            strBuilder.AppendLine("type ApiEndpointNoRequest = Exclude<ApiEndpoint, ApiEndpointWithRequest>\n");
            strBuilder.Append("type ApiResponseType = ApiResponseTypes[ApiEndpoint]");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, strBuilder.ToString());
        }

        private static void WriteApiSocketMap(List<SocketCommandMetadata> commandData, string baseDirectoryPath)
        {
            var filePath = $"{baseDirectoryPath}\\ApiSocketTypeMap.ts";
            var strBuilder = new StringBuilder();
            strBuilder.AppendLine("type ApiSocketResponseTypes = {");
            foreach (var command in commandData)
            {
                strBuilder.AppendLine($"\t'{command.CommandName}': {GetTypeText(command.ResponseType)}");
            }

            strBuilder.AppendLine("}\n");

            strBuilder.AppendLine("type ApiSocketRequestTypes = {");
            foreach (var command in commandData.Where(c => c.ParameterType is not null))
            {
                strBuilder.AppendLine($"\t'{command.CommandName}': {GetTypeText(command.ParameterType)}");
            }

            strBuilder.AppendLine("}\n");
            strBuilder.AppendLine("type ApiSocketCommand = keyof ApiSocketResponseTypes\n");
            strBuilder.AppendLine("type ApiSocketCommandWithRequest = keyof ApiSocketRequestTypes\n");
            strBuilder.AppendLine("type ApiSocketCommandNoRequest = Exclude<ApiSocketCommand, ApiSocketCommandWithRequest>\n");
            strBuilder.Append("type ApiSocketResponseType = ApiSocketResponseTypes[ApiSocketCommand]");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, strBuilder.ToString());
        }

        private static string GetParametersTypeText(EndpointMetaData endpoint)
        {
            if (endpoint.Parameters.Count == 0)
            {
                return "void";
            }
            else if (endpoint.Parameters.Count == 1 && !endpoint.IsGet)
            {
                return endpoint.Parameters[0].TypeText;
            }
            else //Construct anonymous json object type
            {
                return $"{{ {string.Join(", ", endpoint.Parameters.Select(p => $"{p.ParameterName.Decapitalize()}: {p.TypeText}"))} }}";
            }
        }

        private static string GetTypeText(Type? type, bool showNullable = false)
        {
            if (type == null)
            {
                return "undefined";
            }
            else if (type == typeof(int)
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
                return showNullable ? "?number" : "number";
            }
            else if (type == typeof(bool))
            {
                return "boolean";
            }
            else if (type == typeof(bool?))
            {
                return showNullable ? "?boolean" : "boolean";
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
            var interfacesPath = $"{baseDirectoryPath}\\Interfaces";
            var enumPath = $"{baseDirectoryPath}\\Enums.ts";

            if (Directory.Exists(interfacesPath))
                Directory.Delete(interfacesPath, true);

            var fileBuilders = new Dictionary<string, StringBuilder>();
            var subTypes = types.Select(GetSubTypes).ToList();

            foreach (var subTypeList in subTypes)
            {
                types.UnionWith(subTypeList);
            }

            foreach (var type in types.OrderBy(t => GetTypeText(t)))
            {
                var filePath = type.IsEnum
                    ? enumPath
                    : $"{interfacesPath}\\{type.Namespace[(type.Namespace.LastIndexOf('.') + 1)..]}.ts";

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

                if (type.IsEnum)
                    WriteEnumToBuilder(type, builder);
                else
                    WriteInterfaceToBuilder(type, builder);
            }

            foreach ((var path, var builder) in fileBuilders)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, builder.ToString());
            }
        }

        private static void WriteEnumToBuilder(Type type, StringBuilder builder)
        {
            builder.AppendLine($"enum {GetTypeText(type)} {{");
            var values = type.GetEnumValues();
            foreach (var value in values)
            {
                builder.AppendLine($"\t{value.ToString()} = {(int)value},");
            }

            builder.Append('}');
        }

        private static void WriteInterfaceToBuilder(Type type, StringBuilder builder)
        {
            var nullabilityContext = new NullabilityInfoContext();
            builder.AppendLine($"interface {GetInterfaceName(type)} {{");
            if (type.IsGenericTypeDefinition)
            {
                var generics = type.GetGenericArguments().Select((t, i) => (type: t, index: i + 1));
                foreach (var prop in type.GetProperties())
                {
                    var generic = generics.FirstOrDefault(x => x.type == prop.PropertyType);
                    var typeText = generic == default
                        ? GetTypeText(prop.PropertyType, true)
                        : "T" + generic.index.ToString();

                    if (typeText.StartsWith('?'))
                    {
                        builder.AppendLine($"\t{prop.Name.Decapitalize()}?: {typeText.AsSpan(1)};");
                    }
                    else if (nullabilityContext.Create(prop).ReadState == NullabilityState.Nullable)
                    {
                        builder.AppendLine($"\t{prop.Name.Decapitalize()}?: {typeText};");
                    }
                    else
                    {
                        builder.AppendLine($"\t{prop.Name.Decapitalize()}: {typeText};");
                    }
                }
            }
            else
            {
                foreach (var prop in type.GetProperties())
                {
                    var typeText = GetTypeText(prop.PropertyType, true);
                    if (typeText.StartsWith('?'))
                    {
                        builder.AppendLine($"\t{prop.Name.Decapitalize()}?: {typeText.AsSpan(1)};");
                    }
                    else if (nullabilityContext.Create(prop).ReadState == NullabilityState.Nullable)
                    {
                        builder.AppendLine($"\t{prop.Name.Decapitalize()}?: {typeText};");
                    }
                    else
                    {
                        builder.AppendLine($"\t{prop.Name.Decapitalize()}: {typeText};");
                    }
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
                return type.IsEnum || (type.IsClass && !type.IsGenericParameter && type != typeof(string));
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
            else if (type.IsEnum || (type.IsClass && !type.IsGenericParameter && type != typeof(string)))
            {
                return [type];
            }
            else
            {
                return [];
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
                return $"I{type.Name[..type.Name.IndexOf("`")]}<{string.Join(", ", type.GetGenericArguments().Select((arg, i) => "T" + (i + 1)))}>";
            }
            else if (type.IsConstructedGenericType)
            {
                return $"I{type.Name[..type.Name.IndexOf("`")]}<{string.Join(", ", type.GetGenericArguments().Select(t => GetTypeText(t)))}>";
            }
            else
            {
                return $"I{type.Name}";
            }
        }
        private class SocketCommandMetadata
        {
            public string CommandName { get; set; }
            public Type? ResponseType { get; set; }
            public Type? ParameterType { get; set; }
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
