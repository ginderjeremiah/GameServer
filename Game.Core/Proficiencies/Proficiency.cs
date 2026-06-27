namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A proficiency definition — immutable, cached reference-data instance (init-only properties, read-only
    /// collections, mirroring <see cref="Skills.Skill"/>) so the shared cached instance can't be corrupted.
    /// Leveling, bonuses, and unlocks are wired up in later sub-issues; this model carries the authored
    /// definition the gameplay layers read: the level cap, the XP-curve params, and the per-level payouts
    /// (<see cref="Levels"/>).
    /// </summary>
    public class Proficiency
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }

        /// <summary>The path this proficiency is a tier of, and its 0-based position (tier) within it. The
        /// battle XP accrual reads these to route a path's contribution to its current frontier tier and to
        /// scale an off-tier skill's pull by the home-tier falloff.</summary>
        public required int PathId { get; init; }
        public required int PathOrdinal { get; init; }

        /// <summary>The level cap (~10).</summary>
        public required int MaxLevel { get; init; }

        /// <summary>XP-curve parameters; the per-level thresholds are derived from these by the leveling
        /// sub-issue.</summary>
        public required double BaseXp { get; init; }
        public required double XpGrowth { get; init; }

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
        /// The attribute bonuses a player who has reached <paramref name="level"/> in this proficiency has
        /// earned, as battle <see cref="Attributes.Modifiers.AttributeModifier"/>s
        /// (<see cref="EAttributeModifierSource.Proficiency"/> source) fed into the battler at assembly. Per the
        /// "sum of the increments for every level reached" rule (see <see cref="ProficiencyLevel"/>), it is the
        /// modifiers of every authored payout level at or below <paramref name="level"/> — cumulative, so a
        /// higher level strictly adds to a lower one. A level-0 (just-opened) proficiency yields nothing unless
        /// a payout is authored at level 0. The bonuses are baked into the battle snapshot at battle start
        /// (spike #982 decision 7), so a level gained while idling takes effect on the next battle.
        /// </summary>
        public IEnumerable<Attributes.Modifiers.AttributeModifier> ModifiersForLevel(int level) =>
            Levels.Where(l => l.Level <= level)
                .SelectMany(l => l.Modifiers)
                .Select(modifier => modifier.ToAttributeModifier());

        /// <summary>
        /// The authored payout levels crossed by advancing from <paramref name="fromLevel"/> (exclusive) to
        /// <paramref name="toLevel"/> (inclusive) — the milestones this battle's gain newly reached, in
        /// ascending order. Reported on the client push so a level-up surfaces immediately; the effects
        /// themselves (reward skills) are applied by <see cref="RewardSkillsCrossed"/>.
        /// </summary>
        public IReadOnlyList<int> MilestonesCrossed(int fromLevel, int toLevel) =>
            [.. Levels.Where(l => l.Level > fromLevel && l.Level <= toLevel).Select(l => l.Level)];

        /// <summary>
        /// The reward skill ids granted by the milestones crossed advancing from <paramref name="fromLevel"/>
        /// (exclusive) to <paramref name="toLevel"/> (inclusive) — the permanent skills this gain newly earns,
        /// in ascending level order. A milestone with only an attribute bonus (no <see cref="ProficiencyLevel.RewardSkillId"/>)
        /// is bonus-only and contributes nothing here, so the gap between this and <see cref="MilestonesCrossed"/>
        /// is exactly the bonus-only set.
        /// </summary>
        public IReadOnlyList<int> RewardSkillsCrossed(int fromLevel, int toLevel) =>
            [.. Levels.Where(l => l.Level > fromLevel && l.Level <= toLevel && l.RewardSkillId is not null)
                .Select(l => l.RewardSkillId!.Value)];

        /// <summary>True once a player has reached this proficiency's <see cref="MaxLevel"/>.</summary>
        public bool IsMaxed(int level) => level >= MaxLevel;
    }
}
