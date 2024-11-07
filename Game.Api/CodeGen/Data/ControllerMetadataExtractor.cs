using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;

namespace Game.Api.CodeGen.Data
{
    internal class ControllerMetadataExtractor
    {
        private readonly string? _routeTemplate;
        private readonly string? _name;
        public List<EndpointMetadata> Endpoints { get; set; }

        public ControllerMetadataExtractor(Type controller)
        {
            _routeTemplate = controller.GetCustomAttribute<RouteAttribute>()?.Template;
            _name = controller.Name.Replace("Controller", string.Empty);
            Endpoints = GetEndpointMethodsInfo(controller).Select(GenerateEndpointMetadata).ToList();
        }

        private IEnumerable<MethodInfo> GetEndpointMethodsInfo(Type controller)
        {
            var endpointMethods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            return endpointMethods
                .Where(method => method.GetCustomAttribute<NonActionAttribute>() is null
                    && (method.ReturnType.IsAssignableTo(typeof(IApiResponse))
                        || (method.ReturnType.IsAssignableTo(typeof(Task))
                            && method.ReturnType.IsGenericType
                            && method.ReturnType.GetGenericArguments()[0].IsAssignableTo(typeof(IApiResponse))))
                 );
        }

        private EndpointMetadata GenerateEndpointMetadata(MethodInfo endpoint)
        {
            var routeTemplate = _routeTemplate;
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
                ? routeTemplate.Replace("[controller]", _name).Replace("[action]", endpoint.Name)
                : $"/api/{_name}/{endpoint.Name}";

            route = route.TrimStart('/');
            if (route.StartsWith("api/"))
            {
                route = route[3..];
            }

            return new EndpointMetadata(endpoint)
            {
                Endpoint = route.TrimStart('/'),
                IsGet = methodAtt?.HttpMethods?.Contains("GET") ?? true
            };
        }
    }
}
