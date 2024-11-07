using Game.Api.CodeGen.Data;
using Game.Core;
using System.Text;

namespace Game.Api.CodeGen.Writers
{
    internal class ApiMapWriter
    {
        public string TargetDir { get; }

        public ApiMapWriter(string targetDir)
        {
            TargetDir = targetDir;
        }

        public void WriteApiMap(IEnumerable<EndpointMetadata> endpointData)
        {
            var filePath = $"{TargetDir}\\api-type-map.ts";
            var strBuilder = new StringBuilder();
            var allTypes = endpointData
                .SelectMany(e => e.ParameterDescriptors.Append(e.ResponseDescriptor))
                .SelectNotNull()
                .Where(t => t.NeedsInterface);

            if (allTypes.Any())
            {
                var importWriter = new ImportWriter();
                strBuilder.AppendLine(importWriter.GetImportText(allTypes));
            }

            var formatter = new CodeGenTypeFormatter();

            strBuilder.AppendLine("export type ApiResponseTypes = {");
            foreach (var endpoint in endpointData)
            {
                strBuilder.AppendLine($"\t'{endpoint.Endpoint}': {formatter.GetTypeText(endpoint.ResponseDescriptor)}");
            }

            strBuilder.AppendLine("}\n");

            strBuilder.AppendLine("export type ApiRequestTypes = {");
            foreach (var endpoint in endpointData.Where(endp => endp.ParameterDescriptors.Count > 0))
            {
                strBuilder.AppendLine($"\t'{endpoint.Endpoint}': {formatter.GetParametersTypeText(endpoint)}");
            }

            strBuilder.AppendLine("}\n");
            strBuilder.AppendLine("export type ApiEndpoint = keyof ApiResponseTypes\n");
            strBuilder.AppendLine("export type ApiEndpointWithRequest = keyof ApiRequestTypes\n");
            strBuilder.AppendLine("export type ApiEndpointNoRequest = Exclude<ApiEndpoint, ApiEndpointWithRequest>\n");
            strBuilder.Append("export type ApiResponseType = ApiResponseTypes[ApiEndpoint]");

            Directory.CreateDirectory(TargetDir);
            File.WriteAllText(filePath, strBuilder.ToString());
        }
    }
}
