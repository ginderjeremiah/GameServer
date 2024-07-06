namespace GameServer.Models.Enemies
{
    public class NewEnemyModel : IModel
    {
        public double? Cooldown { get; set; }
        public EnemyInstance? EnemyInstance { get; set; }
    }
}
