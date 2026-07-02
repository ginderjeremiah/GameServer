namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an enemy in the reference-data catalogue.</summary>
    public class Enemy : IModel, IHasDesignerNotes
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool IsBoss { get; set; }
        public required IEnumerable<AttributeDistribution> AttributeDistribution { get; set; }
        public required IEnumerable<int> SkillPool { get; set; }
        public required IEnumerable<EnemySpawn> Spawns { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
