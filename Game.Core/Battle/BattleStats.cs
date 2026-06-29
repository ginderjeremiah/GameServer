namespace Game.Core.Battle
{
    public class BattleStats
    {
        public double PlayerDamageDealt { get; set; }
        public double PlayerDamageTaken { get; set; }
        public double PlayerDamageHealed { get; set; }
        public double HighestPlayerAttack { get; set; }
        public int PlayerSkillsUsed { get; set; }

        // Player-only crit/dodge outcomes, accumulated across the battle (enemies never crit/dodge). The damage
        // figures are post-mitigation: crit damage is what the crit hits actually dealt, and dodged damage is
        // the post-mitigation hit avoided.
        public int CriticalHits { get; set; }
        public double CriticalDamageDealt { get; set; }
        public int AttacksDodged { get; set; }
        public double DamageDodged { get; set; }

        public Dictionary<int, SkillStats> SkillStats { get; set; } = [];

        /// <summary>
        /// Per-leaf-type damage the player dealt this battle — the proficiency offense "output book" (spike
        /// #1318), consumed directly by the accrual's offense binding. Sums the same post-mitigation amount each
        /// hit booked into <see cref="PlayerDamageDealt"/>, so a focused build trains its damage type fully while
        /// a dabbled one trains it proportionally. Covers both direct hits and the player's typed DoT damage
        /// dealt (#1338 — a bleed tick is type-routed to Bleed with no source-skill attribution).
        /// </summary>
        public Dictionary<EDamageType, double> TypedDamageDealt { get; set; } = [];

        /// <summary>
        /// Per-leaf-type incoming damage the player was exposed to this battle — the proficiency "incoming book"
        /// (spike #1318) — captured <b>before</b> the player's type-resistance and Defense, so a resist never
        /// throttles its own training signal. Covers both direct hits and typed DoT; a fully dodged hit is
        /// excluded (it was evaded, not mitigated — dodged damage trains evasion instead).
        /// </summary>
        public Dictionary<EDamageType, double> TypedDamageExposure { get; set; } = [];

        /// <summary>
        /// The player's power for this battle — the sum of core additive attribute modifiers, the same measure
        /// <see cref="DefeatRewards"/> uses for the difficulty curve. The effect-based proficiency accrual
        /// normalizes each activity by this (spike #1318), so it must be the identical measure to avoid
        /// double-counting power. Populated at battle completion from <see cref="DefeatRewards.PlayerPower"/>
        /// (victory-only, like the rewards); <c>0</c> until then.
        /// </summary>
        public double PlayerPower { get; set; }

        /// <summary>Accumulates a direct hit of <paramref name="amount"/> into the typed offense book.</summary>
        public void AddTypedDamageDealt(EDamageType type, double amount)
        {
            TypedDamageDealt.TryGetValue(type, out var existing);
            TypedDamageDealt[type] = existing + amount;
        }

        /// <summary>Accumulates a pre-mitigation hit of <paramref name="amount"/> into the typed incoming book.</summary>
        public void AddTypedDamageExposure(EDamageType type, double amount)
        {
            TypedDamageExposure.TryGetValue(type, out var existing);
            TypedDamageExposure[type] = existing + amount;
        }
    }

    public class SkillStats
    {
        public int Uses { get; set; }
        public double TotalDamage { get; set; }
        public double HighestSingleAttack { get; set; }
    }
}
