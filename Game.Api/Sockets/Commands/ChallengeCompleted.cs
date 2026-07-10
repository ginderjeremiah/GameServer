using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// A server-initiated push (never sent by the client): the instance holding the player's socket
    /// receives the emitted <see cref="ChallengeCompletedInfo"/> and forwards its payload to the client.
    /// The payload arrives as the command parameters and is echoed straight back as the response data, so
    /// the client's <c>ChallengeCompleted</c> listener can unlock the rewards locally.
    /// </summary>
    public class ChallengeCompleted : AbstractSocketCommand<ChallengeCompletedModel, ChallengeCompletedModel>, IServerInitiatedCommand
    {
        public override string Name { get; set; } = nameof(ChallengeCompleted);

        public override Task<ApiSocketResponse<ChallengeCompletedModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success(Parameters));
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
