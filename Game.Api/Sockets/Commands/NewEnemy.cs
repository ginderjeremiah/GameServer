using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Services;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    public class NewEnemy : AbstractSocketCommand<NewEnemyModel, NewEnemyRequest>
    {
        private readonly SessionService _sessionService;
        private readonly BattleService _battleService;
        private readonly ILogger<NewEnemy> _logger;

        public override string Name { get; set; } = nameof(NewEnemy);

        public NewEnemy(ILogger<NewEnemy> logger, SessionService sessionService, BattleService battleService)
        {
            _sessionService = sessionService;
            _battleService = battleService;
            _logger = logger;
        }

        public override async Task<ApiSocketResponse<NewEnemyModel>> HandleExecuteAsync(SocketContext context)
        {
            var now = DateTime.UtcNow;
            var state = _sessionService.PlayerState;

            if (state.IsOnCooldown(now))
            {
                return Success(new NewEnemyModel
                {
                    Cooldown = (state.EnemyCooldown - now).TotalMilliseconds
                });
            }

            var player = await _sessionService.LoadPlayer();

            var result = await _battleService.StartBattle(player, state, player.CurrentZoneId, Parameters.NewZoneId);

            _sessionService.SavePlayerState();

            _logger.LogDebug("NewEnemy: (enemyId: {EnemyId}, level: {Level}, seed: {Seed})",
                result.Enemy.Id, result.Enemy.Level, result.Seed);

            return Success(new NewEnemyModel
            {
                EnemyInstance = new EnemyInstance
                {
                    Id = result.Enemy.Id,
                    Level = result.Enemy.Level,
                    Seed = result.Seed,
                    SelectedSkills = result.Enemy.Skills.Select(s => s.Id).ToList(),
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
