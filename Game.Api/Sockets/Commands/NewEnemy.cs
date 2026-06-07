using Game.Abstractions.Contracts;
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

        public override async Task<ApiSocketResponse<NewEnemyModel>> HandleExecuteAsync(SocketContext context)
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

            var player = await context.Session.LoadPlayer();

            var result = await _battleService.StartBattle(player, state, player.CurrentZoneId, Parameters.NewZoneId);

            context.Session.SavePlayerState();

            _logger.LogDebug("NewEnemy: (enemyId: {EnemyId}, level: {Level}, seed: {Seed})",
                result.Enemy.Id, result.Enemy.Level, result.Seed);

            return Success(new NewEnemyModel
            {
                EnemyInstance = new EnemyInstance
                {
                    Id = result.Enemy.Id,
                    Level = result.Enemy.Level,
                    Seed = result.Seed,
                    SelectedSkills = result.Enemy.BattleSkills.Select(s => s.Id).ToList(),
                    Attributes = result.Enemy.GetAttributeModifiers()
                        .Select(m => new BattlerAttribute
                        {
                            AttributeId = m.Attribute,
                            Amount = (decimal)m.Amount,
                        }),
                }
            });
        }
    }
}
