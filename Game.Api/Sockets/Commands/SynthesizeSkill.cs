using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Forges a skill-synthesis recipe (spike #1125, area B): combines the player's owned input skills into the
    /// recipe's result skill and unlocks it. The recipe is identified by its id; all validation is server-side
    /// and authoritative (anti-cheat) — <see cref="SynthesisService"/> delegates to
    /// <see cref="Game.Core.Players.Player.TrySynthesizeSkill"/>, which rejects an unknown/retired recipe, a
    /// missing input, or an unmet proficiency condition with no mutation. Synthesis is non-consumptive and the
    /// unlock is idempotent, so re-synthesizing an owned result simply returns it again. The result skill id is
    /// carried back in the response; being player-initiated, the unlock needs no separate server push.
    /// </summary>
    public class SynthesizeSkill : AbstractSocketCommand<SynthesisResult, int>
    {
        private readonly SynthesisService _synthesisService;

        public override string Name { get; set; } = nameof(SynthesizeSkill);

        public SynthesizeSkill(SynthesisService synthesisService)
        {
            _synthesisService = synthesisService;
        }

        public override async Task<ApiSocketResponse<SynthesisResult>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var resultSkillId = await _synthesisService.SynthesizeSkill(player, Parameters, cancellationToken);

            return resultSkillId is int skillId
                ? Success(new SynthesisResult { ResultSkillId = skillId })
                : ErrorWithData("Unable to synthesize skill.", new SynthesisResult { ResultSkillId = null });
        }
    }
}
