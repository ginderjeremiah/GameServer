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

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
