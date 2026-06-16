using Game.Api.Models.Common;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// A server-initiated push (never sent by the client) telling the client that a different server-pushed
    /// command failed on the server and was dead-lettered, so the client can re-sync the authoritative state
    /// that push would have updated rather than silently diverging. It carries the failed command's name as
    /// the payload and echoes it back as the response data, mirroring <see cref="ChallengeCompleted"/>; the
    /// client only listens for it, so it carries no client request type.
    /// </summary>
    public class ServerCommandFailed : AbstractSocketCommandWithResponseData<ServerCommandFailedModel>, IServerInitiatedCommand
    {
        private ServerCommandFailedModel? _model;

        public override string Name { get; set; } = nameof(ServerCommandFailed);

        public override void SetParameters(string? parameters)
        {
            _model = parameters.Deserialize<ServerCommandFailedModel>()
                ?? throw new ArgumentNullException(nameof(parameters));
        }

        public override Task<ApiSocketResponse<ServerCommandFailedModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            if (_model is null)
            {
                throw new InvalidOperationException("ServerCommandFailed executed without parameters.");
            }

            return Task.FromResult(Success(_model));
        }
    }

    public class ServerCommandFailedInfo : SocketCommandInfo
    {
        public ServerCommandFailedInfo(string failedCommandName) : base(nameof(ServerCommandFailed))
        {
            Parameters = new ServerCommandFailedModel { CommandName = failedCommandName }.Serialize();
        }
    }
}
