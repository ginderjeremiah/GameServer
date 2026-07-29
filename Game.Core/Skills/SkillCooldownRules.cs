namespace Game.Core.Skills
{
    /// <summary>
    /// The authoring rules for <see cref="Skill.CooldownMs"/>, kept as pure predicates so the admin save
    /// guard, the content-health lint, and the Workbench all apply one definition instead of restating the
    /// comparison. Marked <see cref="ClientMirroredAttribute"/> so <see cref="MinSkillCooldownMs"/> is
    /// emitted into <c>game-constants.ts</c> for the Workbench to read (the
    /// <see cref="ContentFieldLengths"/> convention: a hand-copied bound is what drifts).
    /// </summary>
    [ClientMirrored]
    public static class SkillCooldownRules
    {
        /// <summary>
        /// The shortest authorable cooldown: one simulation tick. Below this the two consumers of
        /// <see cref="Skill.CooldownMs"/> stop agreeing, in both directions. The engine charges by
        /// <see cref="GameConstants.MsPerTick"/> per tick and resets on fire
        /// (<see cref="Battle.BattleSkill.Update"/>), so it can never fire faster than once per tick — while
        /// <see cref="Battle.CombatRating"/> prices offense as <c>hit ÷ authoredCooldown</c>, which a sub-tick
        /// cooldown inflates without bound. At exactly <c>0</c> the divergence flips: the rating's
        /// divide-by-zero guard skips the skill entirely, pricing the engine's strongest possible skill as
        /// zero offense.
        /// </summary>
        public const int MinSkillCooldownMs = GameConstants.MsPerTick;

        /// <summary>Whether an authored cooldown is at or above <see cref="MinSkillCooldownMs"/>.</summary>
        public static bool IsValidCooldown(int cooldownMs) => cooldownMs >= MinSkillCooldownMs;
    }
}
