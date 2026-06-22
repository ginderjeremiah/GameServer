namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A proficiency: a mastery track for a category of skills (see the proficiency-system spike,
    /// <c>docs/spikes/982-proficiency-system.md</c>). Static, authored reference data with a zero-based
    /// identity. Leveling and bonus/skill payouts are implemented in later sub-issues; this entity carries
    /// only the authored definition: the level cap, the XP curve params, the per-level bonus/skill payouts,
    /// the prerequisite tree edges, and which skills contribute to it.
    /// </summary>
    public class Proficiency : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string IconPath { get; set; }

        /// <summary>The level cap for this proficiency (~10).</summary>
        public int MaxLevel { get; set; }

        /// <summary>XP-curve parameters; the per-level thresholds are derived from these (interpreted by the
        /// leveling sub-issue). Kept as two params rather than per-level rows since the cap is small.</summary>
        public decimal BaseXp { get; set; }
        public decimal XpGrowth { get; set; }

        /// <summary>True for a root proficiency that is open from character creation (class may override later).</summary>
        public bool StartsUnlocked { get; set; }

        /// <summary>Optional skill granted when this proficiency opens via the tree (a node with no world
        /// skill source, e.g. a synthesized line). Null when the proficiency is seeded by an item/starter skill.</summary>
        public int? SeedSkillId { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual Skill? SeedSkill { get; set; }

        public virtual List<ProficiencyLevelModifier> LevelModifiers { get => field ?? throw new NotLoadedException(nameof(LevelModifiers)); set; }
        public virtual List<ProficiencyLevelReward> LevelRewards { get => field ?? throw new NotLoadedException(nameof(LevelRewards)); set; }
        public virtual List<ProficiencyPrerequisite> Prerequisites { get => field ?? throw new NotLoadedException(nameof(Prerequisites)); set; }
        public virtual List<SkillProficiency> SkillContributions { get => field ?? throw new NotLoadedException(nameof(SkillContributions)); set; }
    }
}
