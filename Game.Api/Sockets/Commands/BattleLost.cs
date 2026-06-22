using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    public class BattleLost : AbstractSocketCommandWithResponseData<BattleLostResponse>
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

            var success = await _battleService.EndBattleLoss(player, state, cancellationToken);

            if (success)
            {
                _logger.LogDebug("BattleLost: Player {PlayerId} lost battle", player.Id);

                // A boss loss returns to the idle farm, so prefetch and bundle the next idle battle — letting
                // the client begin it the instant the post-loss cooldown elapses, hiding the NewEnemy round-trip.
                var next = await _battleService.PrepareNextIdleBattle(player, state, cancellationToken);

                context.Session.SavePlayerState();

                var now = DateTime.UtcNow;
                return Success(new BattleLostResponse
                {
                    Cooldown = (state.EnemyCooldown - now).TotalMilliseconds,
                    NextEnemy = EnemyInstance.FromSource(next),
                    NextZoneId = player.CurrentZoneId,
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
