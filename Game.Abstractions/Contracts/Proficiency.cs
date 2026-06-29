namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a proficiency in the reference-data catalogue. The child
    /// collections are read projections; the identity save ignores them (they are persisted through the
    /// dedicated relationship setters, mirroring the skill editor).</summary>
    public class Proficiency : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string IconPath { get; set; }

        /// <summary>The Aetheric conlang "word of power" (romanization rendered as glyphs, e.g. <c>aenkor</c>),
        /// its <see cref="Pronunciation"/>, and its <see cref="Translation"/> — the three decipher strings the
        /// proficiency screen reveals as the tier levels (thresholds derived, not stored).</summary>
        public required string Word { get; set; }
        public required string Pronunciation { get; set; }
        public required string Translation { get; set; }

        /// <summary>The path this proficiency is a tier of, and its 0-based position (tier) within it.</summary>
        public int PathId { get; set; }
        public int PathOrdinal { get; set; }
        public int MaxLevel { get; set; }
        public decimal BaseXp { get; set; }
        public decimal XpGrowth { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).</summary>
        public DateTime? RetiredAt { get; set; }

        public required IEnumerable<ProficiencyLevelModifier> LevelModifiers { get; set; }
        public required IEnumerable<ProficiencyLevelReward> LevelRewards { get; set; }

        /// <summary>Cross-path gateway prerequisites (within-path order is implicit in <see cref="PathOrdinal"/>).</summary>
        public required IEnumerable<int> PrerequisiteIds { get; set; }
    }
}
