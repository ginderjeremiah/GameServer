using Game.Api.Models.Common;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Syncs the player's active idle-loop mode (idle vs. auto-challenge-boss) to the durable player
    /// aggregate so the offline-rewards simulation can resume the correct loop at next login. Mirrors the
    /// frontend's live auto-fight state. The boss is always the player's current zone's boss, so enabling
    /// validates the current zone as anti-cheat (in circulation, unlocked, has a dedicated boss) exactly like
    /// <see cref="ChallengeBoss"/>; disabling (returning to idle) always succeeds.
    /// </summary>
    public class SetAutoChallengeBoss : AbstractSocketCommandWithParams<bool>
    {
        private readonly BattleService _battleService;

        public override string Name { get; set; } = nameof(SetAutoChallengeBoss);

        public SetAutoChallengeBoss(BattleService battleService)
        {
            _battleService = battleService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _battleService.SetAutoChallengeBoss(player, Parameters, cancellationToken);

            return success ? Success() : Error("Failed to set auto-challenge-boss mode.");
        }
    }
}
