namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A proficiency: a mastery track for a category of skills (see the proficiency-system spike,
    /// <c>docs/spikes/982-proficiency-system.md</c>). Static, authored reference data with a zero-based
    /// identity. Leveling and bonus/skill payouts are implemented in later sub-issues; this entity carries
    /// only the authored definition: its position in a <see cref="Path"/>, the level cap, the XP curve
    /// params, the per-level bonus/skill payouts, and the cross-path prerequisite edges. A freshly-opened
    /// tier's native training skill is re-homed onto the previous tier's max-level
    /// <see cref="ProficiencyLevelReward"/> (skill synthesis, spike #1125, supersedes the former tree-seed skill).
    /// </summary>
    public class Proficiency : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string IconPath { get; set; }

        /// <summary>The proficiency's Aetheric conlang "word of power" — the romanization rendered as glyphs
        /// (e.g. <c>aenkor</c>). Decorative flavour that deciphers as the tier levels: undeciphered glyphs →
        /// <see cref="Pronunciation"/> → <see cref="Translation"/>. The reveal thresholds are derived, not
        /// stored (pronunciation at <c>ceil(MaxLevel / 2)</c>, translation at <c>MaxLevel</c>).</summary>
        public required string Word { get; set; }

        /// <summary>The pronunciation of the <see cref="Word"/> (e.g. <c>AYN-kor</c>), revealed at the
        /// half-level decipher threshold.</summary>
        public required string Pronunciation { get; set; }

        /// <summary>The translation of the <see cref="Word"/> (e.g. <c>The First Flame</c>), revealed when the
        /// tier is maxed.</summary>
        public required string Translation { get; set; }

        /// <summary>The path this proficiency is a tier of (a standalone proficiency is a length-one path).</summary>
        public int PathId { get; set; }

        /// <summary>This proficiency's 0-based position within its <see cref="Path"/> — its tier. Unique per
        /// path; tier <c>N+1</c> opens when tier <c>N</c> is maxed (the within-path order is implicit here,
        /// so no prerequisite edges are authored for it).</summary>
        public int PathOrdinal { get; set; }

        /// <summary>The level cap for this proficiency (~10).</summary>
        public int MaxLevel { get; set; }

        /// <summary>XP-curve parameters; the per-level thresholds are derived from these (interpreted by the
        /// leveling sub-issue). Kept as two params rather than per-level rows since the cap is small.</summary>
        public decimal BaseXp { get; set; }
        public decimal XpGrowth { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual Path Path { get => field ?? throw new NotLoadedException(nameof(Path)); set; }

        public virtual List<ProficiencyLevelModifier> LevelModifiers { get => field ?? throw new NotLoadedException(nameof(LevelModifiers)); set; }
        public virtual List<ProficiencyLevelReward> LevelRewards { get => field ?? throw new NotLoadedException(nameof(LevelRewards)); set; }
        public virtual List<ProficiencyPrerequisite> Prerequisites { get => field ?? throw new NotLoadedException(nameof(Prerequisites)); set; }
    }
}
