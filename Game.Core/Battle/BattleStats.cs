namespace Game.Core.Battle
{
    public class BattleStats
    {
        public double PlayerDamageDealt { get; set; }
        public double PlayerDamageTaken { get; set; }
        public double PlayerDamageHealed { get; set; }
        public double HighestPlayerAttack { get; set; }
        public int PlayerSkillsUsed { get; set; }

        // Player-only crit/dodge/parry outcomes, accumulated across the battle (enemies never
        // crit/dodge/parry). The damage figures are post-mitigation: crit damage is what the crit hits
        // actually dealt, and dodged/parried damage is the post-mitigation hit avoided.
        public int CriticalHits { get; set; }
        public double CriticalDamageDealt { get; set; }
        public int AttacksDodged { get; set; }
        public double DamageDodged { get; set; }
        public int AttacksParried { get; set; }
        public double DamageParried { get; set; }

        /// <summary>
        /// The damage the player's parry counterattacks (ripostes) dealt this battle — the proficiency Riposte
        /// event signal (the <c>Parry</c> activity key, #1457). A <b>direct</b> tally like
        /// <see cref="PlayerReflectedDamageDealt"/> (the counter is new damage the parry enabled, not an overlay
        /// rider on a hit that would have landed anyway), also folded into <see cref="PlayerDamageDealt"/> and
        /// the typed offense book by the shared player-fire pipeline the counter routes through.
        /// </summary>
        public double PlayerCounterDamageDealt { get; set; }

        /// <summary>
        /// The Precision (<c>Crit</c> activity key) training signal — distinct from the player-facing
        /// <see cref="CriticalDamageDealt"/> statistic. Each crit books the hit's booked (post-mitigation,
        /// health-capped — #1482) damage × <c>φ(m − 1)</c>, where <c>m</c> is <see cref="EAttribute.CriticalDamage"/>
        /// and <c>φ</c> is the shared overlay saturation (<see cref="Battle.Battler.NormalizeInvestment"/>) — the
        /// uniform share-claim tally every overlay uses (#1481, superseding the counterfactual normalized marginal
        /// of #1448): proportional to crit-damage investment through <c>φ</c> (which the full-hit
        /// <see cref="CriticalDamageDealt"/> is not), and bounded per battle by the enemy's health pool through
        /// the booked basis.
        /// </summary>
        public double CriticalBonusDealt { get; set; }

        /// <summary>
        /// The reflected damage the player returned to the enemy this battle — the proficiency Retribution
        /// event signal (the <c>Reflect</c> activity key). This is the player-as-defender reflection booked
        /// when the enemy attacks; it is also folded into <see cref="PlayerDamageDealt"/> (reflected damage is
        /// genuine damage the player dealt) but tracked separately here so the accrual trains Retribution on
        /// reflection alone rather than on the untyped damage total. Reflection bypasses mitigation and is
        /// untyped, so — like crit/dodge/heal — it maps straight to a single activity key with no damage-type
        /// routing.
        /// </summary>
        public double PlayerReflectedDamageDealt { get; set; }

        /// <summary>
        /// The Hex (<c>Hex</c> activity key) training signal — the share of the player's landed damage claimed by
        /// an applied vulnerability (#1427, reshaped to the uniform share claim by #1481). <c>v</c> is the
        /// resistance the player's own debuff removed (tracked from the applied effect —
        /// <see cref="Battle.Battler.AppliedVulnerability"/> — so the target's base resistance or its own
        /// resistance buffs can't rob the player of credit). Each direct hit and DoT tick books its booked
        /// (health-capped) damage × <c>φ(v)</c> (<see cref="Battle.Battler.HexBonusForHit"/>). Because the booked
        /// basis sums to at most the enemy's health pool per battle, a fixed investment trains ≈ its coverage
        /// share of that pool × <c>φ(v)</c> regardless of the enemy's mitigation — enemy-independent at the
        /// accrual level, with no counterfactual curve evaluation. Like the crit bonus this is a backend-only side
        /// channel with no parity mirror.
        /// </summary>
        public double HexBonusDealt { get; set; }

        /// <summary>
        /// The Momentum (<c>Momentum</c> activity key) training signal — the share of the player's landed damage
        /// claimed by an applied ramp (#1428, reshaped to the uniform share claim by #1481). A ramp is a stacking
        /// self-buff to one of the attacker's typed amplification attributes; <c>r</c> is the amplification the
        /// buff itself contributed (tracked from the applied effect, isolated from any static amplification the
        /// attacker already carries — <see cref="Battle.Battler.AppliedMomentum"/>). Each direct hit whose type
        /// the ramp amplifies books its booked (health-capped) damage × <c>φ(r)</c>. Direct-hit only: a DoT's
        /// frozen amplification already includes whatever ramp was active when it was cast, but the tally does not
        /// extend to DoT ticks. Backend-only like the other overlay tallies.
        /// </summary>
        public double MomentumBonusDealt { get; set; }

        /// <summary>
        /// The Cull (<c>Cull</c> activity key) training signal — the share of the player's landed damage claimed
        /// by the execute investment (#1430, reshaped to the uniform share claim by #1481). Cull is the one
        /// delivery archetype whose enabler is a genuinely new damage-calc conditional rather than an existing
        /// resistance/amplification attribute: the target's missing-health fraction at the moment of the fire
        /// scales <see cref="EAttribute.ExecuteBonus"/> into that fire's multiplier above 1 (the investment),
        /// applied to the raw damage identically to <c>CriticalDamage</c> before mitigation — that real-damage
        /// multiplier stays parity-critical and is untouched by the tally shape. Each portion books its booked
        /// (health-capped) damage × <c>φ(investment)</c>. A target at full health enables nothing; a target near
        /// death saturates the multiplier toward the full <c>ExecuteBonus</c>. Backend-only like the other
        /// overlay tallies — DoT has no counterpart (there is no per-tick "hit" to execute).
        /// </summary>
        public double CullBonusDealt { get; set; }

        /// <summary>
        /// The Sunder (<c>Sunder</c> activity key) training signal — the share of the player's landed damage
        /// claimed by an applied Toughness debuff (#1429, aligned with the uniform share claim by #1481; Sunder
        /// pioneered the no-counterfactual shape, since the nonlinear Toughness curve has no target-flat
        /// marginal). Each direct hit books its booked (health-capped) damage × <c>φ(investment)</c>, where the
        /// investment is the opponent-applied Toughness reduction (<see cref="Battle.Battler.AppliedSunder"/>)
        /// made dimensionless by the curve's own characteristic magnitude <c>K·attackerLevel</c>
        /// (see <see cref="Battle.Battler.SunderBonusForHit"/>). Direct-hit only: DoT bypasses the Toughness
        /// curve entirely, so a Toughness debuff cannot affect it. Backend-only like the other overlay tallies.
        /// </summary>
        public double SunderBonusDealt { get; set; }

        public Dictionary<int, SkillStats> SkillStats { get; set; } = [];

        /// <summary>
        /// Per-leaf-type damage the player dealt this battle — the proficiency offense "output book" (spike
        /// #1318), consumed directly by the accrual's offense binding. Books each hit's post-mitigation amount
        /// <b>capped at the health it actually removed</b> (#1482) — a killing blow's overkill tail is not
        /// activity, so the per-battle total is bounded by the enemy's health pool and a one-shot on a trivial
        /// enemy cannot mint a power-proportional activity floor. The player-facing aggregates
        /// (<see cref="PlayerDamageDealt"/>, <see cref="HighestPlayerAttack"/>, the per-skill totals) deliberately
        /// keep the full uncapped net. Covers both direct hits and the player's typed DoT damage dealt (#1338 — a
        /// bleed tick is type-routed to Bleed with no source-skill attribution).
        /// </summary>
        public Dictionary<EDamageType, double> TypedDamageDealt { get; set; } = [];

        /// <summary>
        /// Per-leaf-type incoming damage the player was exposed to this battle — the proficiency "incoming book"
        /// (spike #1318) — captured <b>before</b> the player's type-resistance and Toughness. Covers both direct
        /// hits and typed DoT; a fully dodged hit is excluded (it was evaded, not mitigated — dodged damage
        /// trains evasion instead). Paired with <see cref="TypedDamageResistanceMitigated"/>: the accrual
        /// (<c>ProficiencyRewardService</c>) splits this pre-mitigation total into its resistance-blocked and
        /// still-landed components and weights them separately (#1454), so a resist trains faster the more of
        /// this exposure it actually blocks rather than the two being indistinguishable here.
        /// </summary>
        public Dictionary<EDamageType, double> TypedDamageExposure { get; set; } = [];

        /// <summary>
        /// Per-leaf-type portion of <see cref="TypedDamageExposure"/> this battler's own type-resistance blocked
        /// this battle — <see cref="Battle.Battler.TypeResistanceMitigated"/> per direct hit, and the
        /// resistance-only tick reduction per DoT tick. Deliberately excludes the Toughness curve (a generic,
        /// non-typed stat) so only the type-specific resistance investment a resist path actually represents
        /// accelerates that path's training (#1454).
        /// </summary>
        public Dictionary<EDamageType, double> TypedDamageResistanceMitigated { get; set; } = [];

        /// <summary>
        /// The player's power for this battle — the sum of core additive attribute modifiers, the same measure
        /// <see cref="DefeatRewards"/> uses for the difficulty curve. The effect-based proficiency accrual
        /// normalizes each activity by this (spike #1318), so it must be the identical measure to avoid
        /// double-counting power. Populated at battle completion from <see cref="DefeatRewards.PlayerPower"/>
        /// (victory-only, like the rewards); <c>0</c> until then.
        /// </summary>
        public double PlayerPower { get; set; }

        /// <summary>Accumulates a direct hit's or DoT tick's booked <paramref name="amount"/> (already capped at
        /// the health it removed by the caller — #1482) into the typed offense book.</summary>
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

        /// <summary>Accumulates a resistance-blocked <paramref name="amount"/> into the typed resist-mitigated book.</summary>
        public void AddTypedDamageResistanceMitigated(EDamageType type, double amount)
        {
            TypedDamageResistanceMitigated.TryGetValue(type, out var existing);
            TypedDamageResistanceMitigated[type] = existing + amount;
        }

        /// <summary>Accumulates a vulnerability share-claim <paramref name="amount"/> (#1427/#1481) into the
        /// type-neutral Hex signal. Cached as a method group by <see cref="BattleContext"/> so the per-tick DoT
        /// phase can record the enemy's Hex bonus without allocating.</summary>
        public void AddHexBonus(double amount)
        {
            HexBonusDealt += amount;
        }
    }

    public class SkillStats
    {
        public int Uses { get; set; }
        public double TotalDamage { get; set; }
        public double HighestSingleAttack { get; set; }
    }
}
