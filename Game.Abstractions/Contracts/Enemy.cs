namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an enemy in the reference-data catalogue.</summary>
    public class Enemy : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool IsBoss { get; set; }
        public required IEnumerable<AttributeDistribution> AttributeDistribution { get; set; }
        public required IEnumerable<int> SkillPool { get; set; }
        public required IEnumerable<EnemySpawn> Spawns { get; set; }
    }
}
