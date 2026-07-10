using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// A server-initiated push (never sent by the client): the instance holding the player's socket receives
    /// the emitted <see cref="ProficiencyXpGainedInfo"/> and forwards its payload to the client. The payload
    /// arrives as the command parameters and is echoed straight back as the response data, so the client's
    /// <c>ProficiencyXpGained</c> listener can update its proficiency store and surface level-ups/milestones
    /// — mirroring <see cref="ChallengeCompleted"/>.
    /// </summary>
    public class ProficiencyXpGained : AbstractSocketCommand<ProficiencyXpGainedModel, ProficiencyXpGainedModel>, IServerInitiatedCommand
    {
        public override string Name { get; set; } = nameof(ProficiencyXpGained);

        public override Task<ApiSocketResponse<ProficiencyXpGainedModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success(Parameters));
        }
    }

    public class ProficiencyXpGainedInfo : SocketCommandInfo
    {
        public ProficiencyXpGainedInfo(ProficiencyXpGainedModel model) : base(nameof(ProficiencyXpGained))
        {
            Parameters = model.Serialize();
        }
    }
}
