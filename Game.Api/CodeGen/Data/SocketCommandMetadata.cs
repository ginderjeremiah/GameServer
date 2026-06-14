using Game.Api.Sockets.Commands;
using System.Reflection;

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

            // Resolve the response/parameter members against the typed generic bases rather than by raw
            // member name: that way a rename or an added overload of HandleExecute/Parameters surfaces as
            // a missed extraction or a loud error here instead of silently mis-extracting a same-named
            // member, and the generic argument is read with a guarded index.
            if (socketCommand.GetClosedGenericBase(typeof(AbstractSocketCommandWithResponseData<>)) is not null)
            {
                var method = socketCommand.GetMethod(nameof(AbstractSocketCommandWithResponseData<>.HandleExecute));
                if (method is not null && method.ReturnParameter.GetNullabilityInfo().GenericTypeArguments is [var responseArg, ..])
                {
                    ResponseDescriptor = new CodeGenTypeDescriptor(responseArg);
                }
            }

            if (socketCommand.GetClosedGenericBase(typeof(AbstractSocketCommandWithParams<>)) is not null
                || socketCommand.GetClosedGenericBase(typeof(AbstractSocketCommand<,>)) is not null)
            {
                var property = socketCommand.GetProperty(nameof(AbstractSocketCommandWithParams<>.Parameters));
                if (property is not null)
                {
                    ParameterDescriptor = new CodeGenTypeDescriptor(property);
                }
            }
        }
    }
}
