using Game.Api.CodeGen.Data;
using Game.Core;
using System.Text;

namespace Game.Api.CodeGen.Writers
{
    internal class SocketMapWriter
    {
        public string TargetDir { get; set; }

        public SocketMapWriter(string targetDir)
        {
            TargetDir = targetDir;
        }

        public void WriteSocketMap(List<SocketCommandMetadata> commandData)
        {
            var orderedData = commandData.OrderBy(c => c.CommandName).ToList();
            var filePath = $"{TargetDir}\\api-socket-type-map.ts";
            var strBuilder = new StringBuilder();
            var allTypes = orderedData
                .SelectNotNull(c => c.ResponseDescriptor)
                .Concat(orderedData.SelectNotNull(c => c.ParameterDescriptor))
                .Where(d => d.NeedsInterface);

            if (allTypes.Any())
            {
                var importWriter = new ImportWriter();
                strBuilder.AppendLine(importWriter.GetImportText(allTypes));
            }

            var formatter = new CodeGenTypeFormatter();

            strBuilder.AppendLine("export type ApiSocketResponseTypes = {");
            foreach (var command in orderedData)
            {
                strBuilder.AppendLine($"\t'{command.CommandName}': {formatter.GetTypeText(command.ResponseDescriptor)};");
            }

            strBuilder.AppendLine("};\n");

            strBuilder.AppendLine("export type ApiSocketRequestTypes = {");
            foreach (var command in orderedData.Where(c => c.ParameterDescriptor is not null))
            {
                strBuilder.AppendLine($"\t'{command.CommandName}': {formatter.GetTypeText(command.ParameterDescriptor)};");
            }

            strBuilder.AppendLine("};\n");
            strBuilder.AppendLine("export type ApiSocketCommand = keyof ApiSocketResponseTypes;\n");
            strBuilder.AppendLine("export type ApiSocketCommandWithRequest = keyof ApiSocketRequestTypes;\n");
            strBuilder.AppendLine("export type ApiSocketCommandNoRequest = Exclude<ApiSocketCommand, ApiSocketCommandWithRequest>;\n");
            strBuilder.Append("export type ApiSocketResponseType = ApiSocketResponseTypes[ApiSocketCommand];");

            Directory.CreateDirectory(TargetDir);
            File.WriteAllText(filePath, strBuilder.ToString());
        }
    }
}
