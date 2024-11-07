namespace Game.Api.Models.Enemies
{
    public class DefeatEnemyResponse : IModel
    {
        public double Cooldown { get; set; }
        public DefeatRewards? Rewards { get; set; }
    }
}
