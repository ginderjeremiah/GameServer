using Game.Api.Sockets.Commands;

namespace Game.Api.CodeGen.Data
{
    internal class SocketCommandMetadata
    {
        public string CommandName { get; set; }
        public CodeGenTypeDescriptor? ResponseDescriptor { get; set; }
        public CodeGenTypeDescriptor? ParameterDescriptor { get; set; }

        public SocketCommandMetadata(Type socketCommand)
        {
            CommandName = socketCommand.Name;

            var method = socketCommand.GetMethods().FirstOrDefault(m => m.Name == nameof(AbstractSocketCommandWithResponseData<object>.HandleExecute));
            if (method is not null)
            {
                ResponseDescriptor = new CodeGenTypeDescriptor(method.ReturnParameter.GetNullabilityInfo().GenericTypeArguments[0]);
            }

            var property = socketCommand.GetProperties().FirstOrDefault(p => p.Name == nameof(AbstractSocketCommandWithParams<object>.Parameters));
            if (property is not null)
            {
                ParameterDescriptor = new CodeGenTypeDescriptor(property);
            }
        }
    }
}
