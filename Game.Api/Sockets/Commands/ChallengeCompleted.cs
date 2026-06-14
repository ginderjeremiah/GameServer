using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// A server-initiated push (never sent by the client): the instance holding the player's socket
    /// receives the emitted <see cref="ChallengeCompletedInfo"/> and forwards its payload to the client.
    /// The payload arrives as the command parameters and is echoed straight back as the response data, so
    /// the client's <c>ChallengeCompleted</c> listener can unlock the rewards locally. It carries no
    /// request type (the parameters are not a typed <c>Parameters</c> property) since the client only
    /// listens for it.
    /// </summary>
    public class ChallengeCompleted : AbstractSocketCommandWithResponseData<ChallengeCompletedModel>, IServerInitiatedCommand
    {
        private ChallengeCompletedModel? _model;

        public override string Name { get; set; } = nameof(ChallengeCompleted);

        public override void SetParameters(string? parameters)
        {
            _model = parameters.Deserialize<ChallengeCompletedModel>()
                ?? throw new ArgumentNullException(nameof(parameters));
        }

        public override Task<ApiSocketResponse<ChallengeCompletedModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            if (_model is null)
            {
                throw new InvalidOperationException("ChallengeCompleted executed without parameters.");
            }

            return Task.FromResult(Success(_model));
        }
    }

    public class ChallengeCompletedInfo : SocketCommandInfo
    {
        public ChallengeCompletedInfo(ChallengeCompletedModel model) : base(nameof(ChallengeCompleted))
        {
            Parameters = model.Serialize();
        }
    }
}
