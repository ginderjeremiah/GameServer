namespace GameServer.Models.Enemies
{
    public class DefeatEnemy : IModel
    {
        public double Cooldown { get; set; }
        public DefeatRewards? Rewards { get; set; }
    }
}
