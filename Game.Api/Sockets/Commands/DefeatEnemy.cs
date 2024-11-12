using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Services;
using Game.Core.BattleSimulation;
using Game.Core.Sessions;
using EnemyInstance = Game.Core.BattleSimulation.EnemyInstance;
using EnemyInstanceModel = Game.Api.Models.Enemies.EnemyInstance;

namespace Game.Api.Sockets.Commands
{
    public class DefeatEnemy : AbstractSocketCommand<DefeatEnemyResponse, EnemyInstanceModel>
    {
        private Session Session { get; }
        private ILogger<DefeatEnemy> Logger { get; }

        public DefeatEnemy(ILogger<DefeatEnemy> logger, SessionService sessionService)
        {
            Session = sessionService.GetSession();
            Logger = logger;
        }

        public override async Task<ApiSocketResponse<DefeatEnemyResponse>> HandleExecuteAsync(SocketContext context)
        {
            var now = DateTime.UtcNow;
            var attributes = Parameters.Attributes.Select(att => new BattlerAttribute { AttributeId = att.AttributeId, Amount = att.Amount }).ToList();
            var instance = new EnemyInstance(Parameters.Id, Parameters.Level, attributes, Parameters.Seed, Parameters.SelectedSkills);
            if (Session.DefeatEnemy(instance))
            {
                Logger.LogDebug("DefeatEnemy: (currentTime: {CurrentTime}, earliestDefeat: {EarliestDefeatTime}, difference: {TimeDifference} ms)", now.ToString("O"), Session.EarliestDefeat.ToString("O"), (now - Session.EarliestDefeat).TotalMilliseconds);
                Session.EnemyCooldown = now.AddSeconds(5);
                var rewards = await Session.GrantRewards(instance);
                await Session.Save();
                return Success(new DefeatEnemyResponse
                {
                    Cooldown = 5000,
                    Rewards = new Models.Enemies.DefeatRewards(rewards)
                });
            }
            else
            {
                Logger.LogError("DefeatEnemy: (victory: {Victory}, currentTime: {CurrentTime}, earliestDefeat: {EarliestDefeat}, difference: {TimeDifference} ms)", Session.Victory, now.ToString("O"), Session.EarliestDefeat.ToString("O"), (now - Session.EarliestDefeat).TotalMilliseconds);
                return ErrorWithData("Enemy could not be defeated.", new DefeatEnemyResponse
                {
                    Cooldown = (Session.EnemyCooldown - now).TotalMilliseconds
                });
            }
        }
    }
}
