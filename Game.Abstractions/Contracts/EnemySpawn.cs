namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a zone in which an enemy can spawn, with its spawn weight.</summary>
    public class EnemySpawn : IModel
    {
        public int ZoneId { get; set; }
        public int Weight { get; set; }
    }
}
