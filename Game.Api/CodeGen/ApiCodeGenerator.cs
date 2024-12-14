using Game.Api.CodeGen.Data;
using Game.Api.CodeGen.Writers;
using Game.Api.Sockets.Commands;
using Game.Core;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Game.Api.CodeGen
{
    public static class ApiCodeGenerator
    {
        public static void GenerateApiCode(Assembly assembly, string targetDir)
        {
            var controllerTypes = assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(ControllerBase)));
            var endpointMetadata = controllerTypes.SelectMany(c => new ControllerMetadataExtractor(c).Endpoints).ToList();

            var socketCommandTypes = assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(AbstractSocketCommand)) && !type.IsAbstract);
            var socketMetadata = socketCommandTypes.Select(sc => new SocketCommandMetadata(sc)).ToList();

            var apiMapWriter = new ApiMapWriter(targetDir);
            apiMapWriter.WriteApiMap(endpointMetadata);

            var socketMapWriter = new SocketMapWriter(targetDir);
            socketMapWriter.WriteSocketMap(socketMetadata);

            var apiTypeDescriptors = endpointMetadata.SelectMany(e => e.ParameterDescriptors.Append(e.ResponseDescriptor)).SelectNotNull();
            var socketTypeDescriptors = socketMetadata.SelectNotNull(s => s.ParameterDescriptor).Concat(socketMetadata.SelectNotNull(s => s.ResponseDescriptor));

            var apiInterfaceWriter = new ApiInterfaceWriter(targetDir);
            apiInterfaceWriter.WriteApiInterfaces(apiTypeDescriptors.Concat(socketTypeDescriptors));
        }
    }
}
