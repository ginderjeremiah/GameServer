using GameCore;
using GameCore.BattleSimulation;
using GameCore.DataAccess;
using GameCore.Sessions;
using GameServer.Models.Common;
using GameServer.Models.Enemies;
using GameServer.Services;
using EnemyInstance = GameCore.BattleSimulation.EnemyInstance;
using EnemyInstanceModel = GameServer.Models.Enemies.EnemyInstance;

namespace GameServer.Sockets.Commands
{
    public class NewEnemy : AbstractSocketCommand<NewEnemyModel, NewEnemyRequest>
    {
        private Session Session { get; }
        private IApiLogger Logger { get; }
        private IRepositoryManager Repositories { get; }

        public NewEnemy(IRepositoryManager repos, IApiLogger logger, SessionService sessionService)
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

            var zone = Repositories.Zones.GetZone(Session.CurrentZone);
            var level = (int)new Random().NextInt64(zone.LevelMin, zone.LevelMax);
            var enemy = Repositories.Enemies.GetRandomEnemy(zone.Id);
            var seed = (uint)(now.Ticks % uint.MaxValue);
            var enemyInstance = new EnemyInstance()
            {
                Id = enemy.Id,
                Level = level,
                Seed = seed
            };

            foreach (var enemySkill in enemy.EnemySkills)
            {
                enemySkill.Skill = Repositories.Skills.GetSkill(enemySkill.SkillId);
            }

            var simulator = new BattleSimulator(Session, enemy, enemyInstance);
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            Session.SetActiveEnemy(enemyInstance, earliestDefeat, victory);

            Logger.LogDebug($"NewEnemy: {{victory: {victory}, battleTime: {totalMs} ms, now: {now:O}, earliestDefeat: {earliestDefeat:O}}}");

            return Success(new NewEnemyModel
            {
                EnemyInstance = new EnemyInstanceModel(enemyInstance)
            });
        }
    }
}
