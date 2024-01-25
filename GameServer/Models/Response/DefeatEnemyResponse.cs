using GameServer.BattleSimulation;

namespace GameServer.Models.Response
{
    public class DefeatEnemyResponse
    {
        public double Cooldown { get; set; }
        public DefeatRewards? Rewards { get; set; }
    }
}
