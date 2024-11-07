using Game.Api.CodeGen.Data;
using Game.Api.CodeGen.Writers;
using Game.Api.Sockets.Commands;
using Game.Core;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.CodeGen
{
    public static class ApiCodeGenerator
    {
        public static void GenerateApiCode()
        {
            var assembly = typeof(ApiCodeGenerator).Assembly;

            var controllerTypes = assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(ControllerBase)));
            var endpointMetadata = controllerTypes.SelectMany(c => new ControllerMetadataExtractor(c).Endpoints).ToList();

            var socketCommandTypes = assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(AbstractSocketCommand)) && !type.IsAbstract);
            var socketMetadata = socketCommandTypes.Select(sc => new SocketCommandMetadata(sc)).ToList();

            var currentDir = Directory.GetCurrentDirectory();
            var assemblyName = assembly.GetName().Name ?? "Unknown";
            var projectDir = currentDir[..(currentDir.LastIndexOf(assemblyName) - 1)];
            var targetDir = $"{projectDir}\\UI\\new-svelte\\src\\lib\\api";

            var apiMapWriter = new ApiMapWriter(targetDir);
            apiMapWriter.WriteApiMap(endpointMetadata.OrderBy(end => end.Endpoint).ToList());

            var socketMapWriter = new SocketMapWriter(targetDir);
            socketMapWriter.WriteSocketMap(socketMetadata.OrderBy(c => c.CommandName).ToList());

            var apiTypeDescriptors = endpointMetadata.SelectMany(e => e.ParameterDescriptors.Append(e.ResponseDescriptor)).SelectNotNull();
            var socketTypeDescriptors = socketMetadata.SelectNotNull(s => s.ParameterDescriptor).Concat(socketMetadata.SelectNotNull(s => s.ResponseDescriptor));

            var apiInterfaceWriter = new ApiInterfaceWriter(targetDir);
            apiInterfaceWriter.WriteApiInterfaces(apiTypeDescriptors.Concat(socketTypeDescriptors));
        }
    }
}
