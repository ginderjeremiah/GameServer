using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    public class NewEnemy : AbstractSocketCommand<NewEnemyModel, NewEnemyRequest>
    {
        private readonly BattleService _battleService;
        private readonly ILogger<NewEnemy> _logger;

        public override string Name { get; set; } = nameof(NewEnemy);

        public NewEnemy(ILogger<NewEnemy> logger, BattleService battleService)
        {
            _battleService = battleService;
            _logger = logger;
        }

        public override async Task<ApiSocketResponse<NewEnemyModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var state = context.Session.PlayerState;

            if (state.IsOnCooldown(now))
            {
                return Success(new NewEnemyModel
                {
                    Cooldown = (state.EnemyCooldown - now).TotalMilliseconds
                });
            }

            var player = context.Session.Player;

            var result = await _battleService.StartBattle(player, state, player.CurrentZoneId, Parameters.NewZoneId, cancellationToken);

            context.Session.SavePlayerState();

            _logger.LogDebug("NewEnemy: (enemyId: {EnemyId}, level: {Level}, seed: {Seed})",
                result.Enemy.Id, result.Enemy.Level, result.Seed);

            return Success(new NewEnemyModel
            {
                EnemyInstance = EnemyInstance.FromSource(result),
                // The authoritative zone after any lazy relocation, so the client follows a server-side move
                // out of a now-unplayable zone instead of repeatedly requesting the stale one.
                ZoneId = player.CurrentZoneId,
            });
        }
    }
}
