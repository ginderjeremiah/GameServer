using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Services;
using Game.Core.Battle;
using EnemyInstanceModel = Game.Api.Models.Enemies.EnemyInstance;

namespace Game.Api.Sockets.Commands
{
    public class NewEnemy : AbstractSocketCommand<NewEnemyModel, NewEnemyRequest>
    {
        private SessionService SessionService { get; }
        private ILogger<NewEnemy> Logger { get; }
        private IRepositoryManager Repositories { get; }

        public override string Name { get; set; } = nameof(NewEnemy);

        public NewEnemy(IRepositoryManager repos, ILogger<NewEnemy> logger, SessionService sessionService)
        {
            SessionService = sessionService;
            Logger = logger;
            Repositories = repos;
        }

        public override ApiSocketResponse<NewEnemyModel> HandleExecute(SocketContext context)
        {
            var now = DateTime.UtcNow;
            if (SessionService.PlayerState.EnemyCooldown > now)
            {
                return Success(new NewEnemyModel
                {
                    Cooldown = (SessionService.PlayerState.EnemyCooldown - now).TotalMilliseconds
                });
            }

            if (Parameters.NewZoneId.HasValue)
            {
                var newZone = Repositories.Zones.GetZone(Parameters.NewZoneId.Value) ?? throw new InvalidOperationException("Failed to load zone");
                SessionService.Player.CurrentZone = newZone;
            }

            var rng = new Random();
            var zone = SessionService.Player.CurrentZone;
            var level = rng.Next(zone.LevelMin, zone.LevelMax);
            var enemy = zone.EnemyTable.GetRandomValue().CloneWithRandomSkills(rng);
            var seed = (uint)(now.Ticks % uint.MaxValue);

            var simulator = new BattleSimulator(SessionService.Player, enemy, seed);
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            Session.SetActiveEnemy(enemy, earliestDefeat, victory);

            Logger.LogDebug("NewEnemy: (victory: {Victory}, battleTime: {BattleTime} ms, currentTime: {CurrentTime}, earliestDefeat: {EarliestDefeat})", victory, totalMs, now.ToString("O"), earliestDefeat.ToString("O"));

            return Success(new NewEnemyModel
            {
                EnemyInstance = new EnemyInstanceModel(enemy)
            });
        }
    }
}
