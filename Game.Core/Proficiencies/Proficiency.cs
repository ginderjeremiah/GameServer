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

        /// <summary>
        /// The XP required to advance from <paramref name="level"/> to <paramref name="level"/> + 1, derived
        /// from the authored curve params: <c>BaseXp × XpGrowth^level</c> (so <c>BaseXp</c> is the cost of the
        /// first level and <c>XpGrowth</c> the per-level multiplier). Rounded to the persisted XP scale
        /// (numeric(18,3)) so the threshold and the stored XP compare on the same grid.
        /// </summary>
        public decimal XpForLevel(int level) =>
            Math.Round((decimal)(BaseXp * Math.Pow(XpGrowth, level)), 3, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Applies an XP gain to a player's current progress in this proficiency, leveling up across as many
        /// thresholds as the gain spans. <see cref="PlayerProficiency.Xp"/> is the residual XP within the
        /// current level, so each level-up subtracts that level's threshold; leveling stops at
        /// <see cref="MaxLevel"/>, where the residual is pinned to 0 (a maxed proficiency banks no overflow,
        /// matching its permanent, non-decaying bonuses). Pure: it computes the new state rather than
        /// mutating, so the persistence seam (<see cref="PlayerProficiency.SetProficiencyProgress"/>) writes
        /// absolute values and a re-applied write-behind event converges.
        /// </summary>
        public (int Level, decimal Xp) ApplyXp(int currentLevel, decimal currentXp, decimal xpGain)
        {
            var level = currentLevel;
            var xp = currentXp + xpGain;

            while (level < MaxLevel)
            {
                var threshold = XpForLevel(level);
                if (xp < threshold)
                {
                    break;
                }

                xp -= threshold;
                level++;
            }

            if (level >= MaxLevel)
            {
                level = MaxLevel;
                xp = 0m;
            }

            return (level, xp);
        }

        /// <summary>
        /// The authored payout levels crossed by advancing from <paramref name="fromLevel"/> (exclusive) to
        /// <paramref name="toLevel"/> (inclusive) — the milestones this battle's gain newly reached, in
        /// ascending order. The effects themselves (reward skills, child-node unlocks) are applied by the
        /// milestone sub-issue (#1118); this just reports which were crossed for the client push.
        /// </summary>
        public IReadOnlyList<int> MilestonesCrossed(int fromLevel, int toLevel) =>
            [.. Levels.Where(l => l.Level > fromLevel && l.Level <= toLevel).Select(l => l.Level)];
    }
}
