using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// A server-initiated push (never sent by the client): the instance holding the player's socket receives
    /// the emitted <see cref="ProficiencyXpGainedInfo"/> and forwards its payload to the client. The payload
    /// arrives as the command parameters and is echoed straight back as the response data, so the client's
    /// <c>ProficiencyXpGained</c> listener can update its proficiency store and surface level-ups/milestones.
    /// It carries no request type (the parameters are not a typed <c>Parameters</c> property) since the client
    /// only listens for it — mirroring <see cref="ChallengeCompleted"/>.
    /// </summary>
    public class ProficiencyXpGained : AbstractSocketCommandWithResponseData<ProficiencyXpGainedModel>, IServerInitiatedCommand
    {
        private ProficiencyXpGainedModel? _model;

        public override string Name { get; set; } = nameof(ProficiencyXpGained);

        public override void SetParameters(string? parameters)
        {
            _model = parameters.Deserialize<ProficiencyXpGainedModel>()
                ?? throw new ArgumentNullException(nameof(parameters));
        }

        public override Task<ApiSocketResponse<ProficiencyXpGainedModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            if (_model is null)
            {
                throw new InvalidOperationException("ProficiencyXpGained executed without parameters.");
            }

            return Task.FromResult(Success(_model));
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
