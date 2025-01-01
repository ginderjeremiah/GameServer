using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Services;
using Game.Core;
using Game.Core.BattleSimulation;
using Game.Core.DataAccess;
using Game.Core.Sessions;
using EnemyInstance = Game.Core.BattleSimulation.EnemyInstance;
using EnemyInstanceModel = Game.Api.Models.Enemies.EnemyInstance;

namespace Game.Api.Sockets.Commands
{
    public class NewEnemy : AbstractSocketCommand<NewEnemyModel, NewEnemyRequest>
    {
        private Session Session { get; }
        private ILogger<NewEnemy> Logger { get; }
        private IRepositoryManager Repositories { get; }

        public override string Name { get; set; } = nameof(NewEnemy);

        public NewEnemy(IRepositoryManager repos, ILogger<NewEnemy> logger, SessionService sessionService)
        {
            Session = sessionService.GetSession();
            Logger = logger;
            Repositories = repos;
        }

        public override ApiSocketResponse<NewEnemyModel> HandleExecute(SocketContext context)
        {
            var now = DateTime.UtcNow;
            if (Session.EnemyCooldown > now)
            {
                return Success(new NewEnemyModel
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }

            if (Parameters.NewZoneId.HasValue && Repositories.Zones.ValidateZoneId(Parameters.NewZoneId.Value))
            {
                Session.CurrentZone = Parameters.NewZoneId.Value;
            }

            var zone = Repositories.Zones.GetZone(Session.CurrentZone) ?? throw new InvalidOperationException("Failed to load zone");
            var level = (int)new Random().NextInt64(zone.LevelMin, zone.LevelMax);
            var enemy = Repositories.Enemies.GetRandomEnemy(zone.Id);
            var seed = (uint)(now.Ticks % uint.MaxValue);

            var rng = new Random();
            var selectedSkills = enemy.EnemySkills.OrderBy(es => rng.Next()).Take(4).Select(es => es.SkillId).OrderBy(id => id).ToList();
            var attributes = enemy.AttributeDistributions.Select(distribution => new BattlerAttribute(distribution, level)).ToList();
            var enemyInstance = new EnemyInstance(enemy.Id, level, attributes, seed, selectedSkills);
            var enemySkills = enemyInstance.SelectedSkills.SelectNotNull(Repositories.Skills.GetSkill);

            var simulator = new BattleSimulator(Session, enemyInstance, enemySkills);
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            Session.SetActiveEnemy(enemyInstance, earliestDefeat, victory);

            Logger.LogDebug("NewEnemy: (victory: {Victory}, battleTime: {BattleTime} ms, currentTime: {CurrentTime}, earliestDefeat: {EarliestDefeat})", victory, totalMs, now.ToString("O"), earliestDefeat.ToString("O"));

            return Success(new NewEnemyModel
            {
                EnemyInstance = new EnemyInstanceModel(enemyInstance)
            });
        }
    }
}
