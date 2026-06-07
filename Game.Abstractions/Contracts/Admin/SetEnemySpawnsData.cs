namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full set of zone spawns to associate with a single enemy (<see cref="EnemyId"/>).</summary>
    public class SetEnemySpawnsData
    {
        public int EnemyId { get; set; }

        public required List<EnemySpawn> Spawns { get; set; }
    }
}
