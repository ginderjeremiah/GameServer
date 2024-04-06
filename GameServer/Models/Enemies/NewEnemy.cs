namespace GameServer.Models.Enemies
{
    public class NewEnemy : IModel
    {
        public double? Cooldown { get; set; }
        public EnemyInstance? EnemyInstance { get; set; }
    }
}
