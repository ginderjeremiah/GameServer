namespace Game.Api.Models.Enemies
{
    public class SetEnemySpawnsData
    {
        public int EnemyId { get; set; }

        public required List<EnemySpawn> Spawns { get; set; }
    }
}
