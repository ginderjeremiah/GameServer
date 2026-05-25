using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Application.Services;
using Game.Core.Battle;
using EnemyInstanceModel = Game.Api.Models.Enemies.EnemyInstance;

namespace Game.Api.Sockets.Commands
{
    public class DefeatEnemy : AbstractSocketCommand<DefeatEnemyResponse, EnemyInstanceModel>
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
            var now = DateTime.UtcNow;
            var state = context.Session.PlayerState;
            var player = await context.Session.LoadPlayer();

            if (!state.ActiveEnemyId.HasValue)
            {
                return ErrorWithData("No active enemy.", new DefeatEnemyResponse
                {
                    Cooldown = 0,
                });
            }

            var rng = new Mulberry32(state.BattleSeed ?? 0);
            var rewards = await _battleService.TryDefeatEnemy(
                player, state,
                state.ActiveEnemyId.Value,
                state.ActiveEnemyLevel ?? 1,
                rng);

            if (rewards is not null)
            {
                _logger.LogDebug("DefeatEnemy: (currentTime: {CurrentTime}, exp: {Exp})",
                    now.ToString("O"), rewards.ExpReward);

                context.Session.SavePlayerState();

                return Success(new DefeatEnemyResponse
                {
                    Cooldown = 5000,
                    Rewards = new Models.Enemies.DefeatRewards(rewards),
                });
            }
            else
            {
                _logger.LogError("DefeatEnemy: (victory: {Victory}, currentTime: {CurrentTime}, earliestDefeat: {EarliestDefeat})",
                    state.Victory, now.ToString("O"), state.EarliestDefeat.ToString("O"));

                return ErrorWithData("Enemy could not be defeated.", new DefeatEnemyResponse
                {
                    Cooldown = state.IsOnCooldown(now) ? (state.EnemyCooldown - now).TotalMilliseconds : 0,
                });
            }
        }
    }
}
