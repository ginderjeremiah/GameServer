namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A proficiency definition — immutable, cached reference-data instance (init-only properties, read-only
    /// collections, mirroring <see cref="Skills.Skill"/>) so the shared cached instance can't be corrupted.
    /// Leveling, bonuses, and unlocks are wired up in later sub-issues; this model carries the authored
    /// definition the gameplay layers read: the level cap, the XP-curve params, the per-level payouts
    /// (<see cref="Levels"/>), the prerequisite tree edges, and the optional tree-seed skill.
    /// </summary>
    public class Proficiency
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }

        /// <summary>The level cap (~10).</summary>
        public required int MaxLevel { get; init; }

        /// <summary>XP-curve parameters; the per-level thresholds are derived from these by the leveling
        /// sub-issue.</summary>
        public required double BaseXp { get; init; }
        public required double XpGrowth { get; init; }

        /// <summary>True for a root proficiency open from character creation.</summary>
        public required bool StartsUnlocked { get; init; }

        /// <summary>Optional skill granted when this proficiency opens via the tree (a node with no world
        /// skill source); null when seeded by an item/starter skill.</summary>
        public required int? SeedSkillId { get; init; }

        /// <summary>The proficiencies that must be maxed before this one opens.</summary>
        public required IReadOnlyList<int> PrerequisiteIds { get; init; }

        /// <summary>The authored levels that carry a payout (a bonus and/or a reward skill), ascending by
        /// level. Sparse — a level with no authored payout is absent.</summary>
        public required IReadOnlyList<ProficiencyLevel> Levels { get; init; }
    }
}
