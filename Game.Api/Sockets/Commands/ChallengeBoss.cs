using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Starts a deterministic battle against the current (or requested) zone's dedicated boss. Mirrors
    /// <see cref="NewEnemy"/> but for the always-available "Challenge Boss" action: there is no cooldown
    /// gate, and the response reuses <see cref="NewEnemyModel"/>. Returns an error when the zone has no
    /// dedicated boss authored.
    /// </summary>
    public class ChallengeBoss : AbstractSocketCommand<NewEnemyModel, ChallengeBossRequest>
    {
        private readonly BattleService _battleService;
        private readonly ILogger<ChallengeBoss> _logger;

        public override string Name { get; set; } = nameof(ChallengeBoss);

        public ChallengeBoss(ILogger<ChallengeBoss> logger, BattleService battleService)
        {
            _battleService = battleService;
            _logger = logger;
        }

        public override async Task<ApiSocketResponse<NewEnemyModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var state = context.Session.PlayerState;
            var player = context.Session.Player;
            // Deliberate asymmetry with the auto-loop sibling SetAutoChallengeBoss (which always uses the
            // current zone): the interactive challenge lets the client name a zone so a player can re-farm a
            // previously-cleared boss without walking back. This can't reach locked content — StartBossBattle
            // still gates on the zone being unlocked and not retired — so a client can only name a zone it has
            // already unlocked.
            var zoneId = Parameters.ZoneId ?? player.CurrentZoneId;

            var result = await _battleService.StartBossBattle(player, state, zoneId, Parameters.ClientBattleMs, cancellationToken);

            if (result is null)
            {
                return ErrorWithData("This zone has no boss to challenge.", new NewEnemyModel());
            }

            await context.Session.SavePlayerStateAsync(cancellationToken);

            _logger.LogDebug("ChallengeBoss: (zoneId: {ZoneId}, enemyId: {EnemyId}, level: {Level}, seed: {Seed})",
                zoneId, result.Enemy.Id, result.Enemy.Level, result.Seed);

            return Success(new NewEnemyModel
            {
                EnemyInstance = EnemyInstance.FromSource(result)
            });
        }
    }
}
