namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a skill in the reference-data catalogue.</summary>
    public class Skill : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public required IEnumerable<AttributeMultiplier> DamageMultipliers { get; set; }
        public required IEnumerable<SkillEffect> Effects { get; set; }
        public required string Description { get; set; }
        public int CooldownMs { get; set; }
        public required string IconPath { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
