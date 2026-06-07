namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an enemy that spawns in a zone, with its spawn weight.</summary>
    public class ZoneEnemy : IModel
    {
        public int EnemyId { get; set; }
        public int Weight { get; set; }
    }
}
