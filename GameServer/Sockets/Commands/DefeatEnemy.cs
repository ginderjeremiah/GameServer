using GameCore;
using GameCore.BattleSimulation;
using GameCore.Sessions;
using GameServer.Models.Common;
using GameServer.Models.Enemies;
using GameServer.Services;
using EnemyInstance = GameCore.BattleSimulation.EnemyInstance;
using EnemyInstanceModel = GameServer.Models.Enemies.EnemyInstance;

namespace GameServer.Sockets.Commands
{
    public class DefeatEnemy : AbstractSocketCommand<DefeatEnemyResponse, EnemyInstanceModel>
    {
        private Session Session { get; }
        private IApiLogger Logger { get; }

        public DefeatEnemy(IApiLogger logger, SessionService sessionService)
        {
            Session = sessionService.GetSession();
            Logger = logger;
        }

        public override ApiSocketResponse<DefeatEnemyResponse> HandleExecute()
        {
            var now = DateTime.UtcNow;
            var instance = new EnemyInstance
            {
                Id = Parameters.Id,
                Level = Parameters.Level,
                Seed = Parameters.Seed,
                SelectedSkills = Parameters.SelectedSkills,
                Attributes = Parameters.Attributes.Select(att => new BattlerAttribute { AttributeId = att.AttributeId, Amount = att.Amount }).ToList()
            };

            if (Session.DefeatEnemy(instance))
            {
                Logger.LogDebug($"DefeatEnemy: {{currentTime: {now:O}, earliestDefeat: {Session.EarliestDefeat:O}, difference: {(now - Session.EarliestDefeat).TotalMilliseconds} ms}}");
                Session.EnemyCooldown = now.AddSeconds(5);
                var rewards = Session.GrantRewards(instance);
                return Success(new DefeatEnemyResponse
                {
                    Cooldown = 5000,
                    Rewards = new Models.Enemies.DefeatRewards(rewards)
                });
            }
            else
            {
                Logger.LogError($"DefeatEnemy: {{victory: {Session.Victory}, currentTime: {now:O}, earliestDefeat: {Session.EarliestDefeat:O}, difference: {(now - Session.EarliestDefeat).TotalMilliseconds} ms}}");
                return ErrorWithData("Enemy could not be defeated.", new DefeatEnemyResponse
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }
        }
    }
}
