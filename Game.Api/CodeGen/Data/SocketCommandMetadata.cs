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
                // HandleExecuteAsync returns Task<ApiSocketResponse<T>>; peel the Task and the response
                // envelope to reach T (the data type the generated client surfaces).
                var method = socketCommand.GetMethod(nameof(AbstractSocketCommandWithResponseData<>.HandleExecuteAsync));
                if (method is not null
                    && method.ReturnParameter.GetNullabilityInfo().GenericTypeArguments is [var responseEnvelope]
                    && responseEnvelope.GenericTypeArguments is [var responseArg, ..])
                {
                    ResponseDescriptor = new CodeGenTypeDescriptor(responseArg);
                }
            }

            // A server-initiated command (IServerInitiatedCommand) may bind its Parameters from a typed base
            // purely to reuse DeserializeParameters<T>'s malformed-payload classification, but the client only
            // ever listens for it and can never send it (SocketCommandFactory.IsServerInitiatedOnly rejects an
            // inbound attempt). It must never surface in ApiSocketRequestTypes/ApiSocketCommandWithRequest,
            // which the frontend uses to type its client->server sends.
            if (!socketCommand.IsAssignableTo(typeof(IServerInitiatedCommand))
                && (socketCommand.GetClosedGenericBase(typeof(AbstractSocketCommandWithParams<>)) is not null
                    || socketCommand.GetClosedGenericBase(typeof(AbstractSocketCommand<,>)) is not null))
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
