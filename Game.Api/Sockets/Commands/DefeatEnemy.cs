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

        public override async Task<ApiSocketResponse<DefeatEnemyResponse>> HandleExecuteAsync(SocketContext context)
        {
            var state = context.Session.PlayerState;
            var player = await context.Session.LoadPlayer();

            if (!state.HasActiveBattle)
            {
                return ErrorWithData("No active enemy.", new DefeatEnemyResponse
                {
                    Cooldown = 0,
                });
            }

            var claimedTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(Parameters.Timestamp).UtcDateTime;

            var rewards = await _battleService.EndBattleVictory(player, state, claimedTimestamp);

            if (rewards is not null)
            {
                _logger.LogDebug("DefeatEnemy: (timestamp: {Timestamp}, exp: {Exp})",
                    claimedTimestamp.ToString("O"), rewards.ExpReward);

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
                _logger.LogError("DefeatEnemy: Could not defeat enemy (timestamp: {Timestamp})",
                    claimedTimestamp.ToString("O"));

                var now = DateTime.UtcNow;
                return ErrorWithData("Enemy could not be defeated.", new DefeatEnemyResponse
                {
                    Cooldown = state.IsOnCooldown(now) ? (state.EnemyCooldown - now).TotalMilliseconds : 0,
                });
            }
        }
    }
}
