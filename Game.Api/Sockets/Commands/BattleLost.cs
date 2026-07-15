using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    public class BattleLost : AbstractSocketCommand<BattleLostResponse, BattleLostRequest>
    {
        private readonly BattleService _battleService;
        private readonly ILogger<BattleLost> _logger;

        public override string Name { get; set; } = nameof(BattleLost);

        public BattleLost(ILogger<BattleLost> logger, BattleService battleService)
        {
            _battleService = battleService;
            _logger = logger;
        }

        public override async Task<ApiSocketResponse<BattleLostResponse>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var state = context.Session.PlayerState;
            var player = context.Session.Player;

            if (!state.HasActiveBattle)
            {
                return ErrorWithData("No active battle.", new BattleLostResponse
                {
                    Cooldown = 0,
                });
            }

            var success = await _battleService.EndBattleLoss(player, state, Parameters.ClientTotalMs, cancellationToken);

            if (success)
            {
                _logger.LogDebug("BattleLost: Player {PlayerId} lost battle", player.Id);

                // A boss loss returns to the idle farm, so prefetch and bundle the next idle battle — letting
                // the client begin it the instant the post-loss cooldown elapses, hiding the NewEnemy round-trip.
                // The prefetch is best-effort: the loss is already durably recorded and its battle-cleared
                // PlayerState is saved below regardless, so a prefetch failure must not strand that resolved
                // state (which a reconnect would re-abandon, re-recording the completion). The client
                // round-trips NewEnemy when none is bundled.
                var next = await _battleService.TryPrepareNextIdleBattle(player, state, cancellationToken);

                await context.Session.SavePlayerStateAsync(cancellationToken);

                var now = DateTime.UtcNow;
                return Success(new BattleLostResponse
                {
                    Cooldown = (state.EnemyCooldown - now).TotalMilliseconds,
                    NextEnemy = next is not null ? EnemyInstance.FromSource(next) : null,
                    NextZoneId = next is not null ? player.CurrentZoneId : null,
                });
            }
            else
            {
                return ErrorWithData("Battle was not a loss.", new BattleLostResponse
                {
                    Cooldown = 0,
                });
            }
        }
    }
}
