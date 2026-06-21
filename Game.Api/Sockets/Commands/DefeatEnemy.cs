using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    public class DefeatEnemy : AbstractSocketCommand<DefeatEnemyResponse, DefeatEnemyRequest>
    {
        private readonly BattleService _battleService;
        private readonly ILogger<DefeatEnemy> _logger;

        public override string Name { get; set; } = nameof(DefeatEnemy);

        public DefeatEnemy(ILogger<DefeatEnemy> logger, BattleService battleService)
        {
            _battleService = battleService;
            _logger = logger;
        }

        public override async Task<ApiSocketResponse<DefeatEnemyResponse>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var state = context.Session.PlayerState;
            var player = context.Session.Player;

            if (!state.HasActiveBattle)
            {
                return ErrorWithData("No active enemy.", new DefeatEnemyResponse
                {
                    Cooldown = 0,
                });
            }

            var rewards = await _battleService.EndBattleVictory(player, state, Parameters.ClientTotalMs, cancellationToken);

            if (rewards is not null)
            {
                _logger.LogDebug("DefeatEnemy: player {PlayerId} defeated enemy (exp: {Exp})",
                    player.Id, rewards.ExpReward);

                context.Session.SavePlayerState();

                var now = DateTime.UtcNow;
                return Success(new DefeatEnemyResponse
                {
                    Cooldown = (state.EnemyCooldown - now).TotalMilliseconds,
                    Rewards = new DefeatRewards(rewards),
                });
            }
            else
            {
                // BattleService logs the specific rejection reason (and its diagnostics) at the source.
                var now = DateTime.UtcNow;
                return ErrorWithData("Enemy could not be defeated.", new DefeatEnemyResponse
                {
                    Cooldown = state.IsOnCooldown(now) ? (state.EnemyCooldown - now).TotalMilliseconds : 0,
                });
            }
        }
    }
}
