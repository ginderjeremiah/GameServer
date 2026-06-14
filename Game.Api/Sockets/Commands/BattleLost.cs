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
            var player = await context.Session.LoadPlayer();

            if (!state.HasActiveBattle)
            {
                return ErrorWithData("No active battle.", new BattleLostResponse
                {
                    Cooldown = 0,
                });
            }

            var success = await _battleService.EndBattleLoss(player, state);

            if (success)
            {
                _logger.LogDebug("BattleLost: Player {PlayerId} lost battle", player.Id);

                context.Session.SavePlayerState();

                var now = DateTime.UtcNow;
                return Success(new BattleLostResponse
                {
                    Cooldown = (state.EnemyCooldown - now).TotalMilliseconds,
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
