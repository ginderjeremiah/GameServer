namespace Game.Core.Proficiencies
{
    /// <summary>
    /// Maps a contributing skill's rarity tier to its proficiency-XP weight (the <c>skillTierWeight</c> in the
    /// fixed-pie split, spike #982 decision 4). Tier weight paces deep proficiencies so they aren't an eternal
    /// trickle-grind on a starter skill — but that weighting is deferred until skill rarity (#979) lands, so
    /// every tier weighs a flat <c>1</c> for now (#1123 is the tracker that turns this into the real curve).
    /// The accrual already reads <see cref="Skills.Skill.Rarity"/> and routes it through here, so enabling the
    /// weighting is a one-method change rather than a new call-site.
    /// </summary>
    public static class ProficiencyTierWeight
    {
        /// <summary>The tier weight for <paramref name="rarity"/> — flat <c>1</c> until #979 lands.</summary>
        public static double For(ERarity rarity) => 1.0;
    }
}
