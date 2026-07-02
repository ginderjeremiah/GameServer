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

        /// <summary>
        /// The Precision (<c>Crit</c> activity key) training signal — distinct from the player-facing
        /// <see cref="CriticalDamageDealt"/> statistic. It is the <b>normalized marginal</b> crit bonus: for each
        /// crit, the extra post-mitigation damage over the vanilla (non-crit) hit — measured against that fixed
        /// baseline, <c>N(crit) − N₀ = baseline × (m − 1)</c> — with the concave-saturating normalization
        /// <c>φ(a) = a / (1 + a)</c> applied to the crit-damage investment <c>(m − 1)</c>, so a bigger crit trains
        /// Precision more but a single crit is bounded at one baseline hit (spike #1398 → the delivery-archetype
        /// normalized-marginal tally, reference implementation #1448). This makes the training signal proportional
        /// to crit-damage investment, which the full-hit <see cref="CriticalDamageDealt"/> is not.
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
        /// The Hex (<c>Hex</c> activity key) training signal — the normalized-marginal damage the player's
        /// applied vulnerability enabled this battle (spike #1398 → the overlay tally shape, reference #1448).
        /// <c>v</c> is the resistance the player's own debuff removed (tracked from the applied effect, not diffed
        /// against a baseline), so an enemy's base resistance or its own resistance buffs can't rob the player of
        /// credit for the work the debuff did. For each hit and DoT tick the extra damage that reduction let
        /// through vs. the same hit without the debuff — <c>D × v</c> in the normal region — is booked with the
        /// same <c>/(1 + v)</c> saturation the crit bonus uses, on the debuff strength <c>v</c>. Because the
        /// enabled damage is the pre-resistance amount, it is flat in the enemy's resistance (no resist-farming),
        /// and the saturation makes it proportional to how hard the player invested in the debuff. Like the crit
        /// bonus this is a backend-only side channel with no parity mirror.
        /// </summary>
        public double HexBonusDealt { get; set; }

        /// <summary>
        /// The Momentum (<c>Momentum</c> activity key) training signal — the normalized-marginal damage the
        /// player's applied ramp enabled this battle (spike #1398 → the overlay tally shape, reference #1448). A
        /// ramp is a stacking self-buff to one of the attacker's typed amplification attributes; <c>r</c> is the
        /// amplification the buff itself contributed (tracked from the applied effect, isolated from any static
        /// amplification the attacker already carries — <see cref="Battle.Battler.AppliedMomentum"/>). For each
        /// hit the extra damage that amplification enabled — the live post-amplification hit minus the same hit
        /// without the ramp's contribution, both carried through the defender's mitigation — is booked with the
        /// same <c>/(1 + r)</c> saturation the crit and Hex bonuses use, on the ramp's own magnitude. Unlike Hex
        /// this is <b>not</b> flat in the defender's mitigation (Momentum amplifies the attacker's output, so a
        /// tougher target mitigates the ramp bonus exactly as it mitigates the rest of the hit — the same
        /// property the crit bonus has). Backend-only like the other overlay tallies.
        /// </summary>
        public double MomentumBonusDealt { get; set; }

        /// <summary>
        /// The Cull (<c>Cull</c> activity key) training signal — the normalized-marginal execute bonus an
        /// authored <see cref="EAttribute.ExecuteBonus"/> enabled this battle (spike #1398 → the overlay tally
        /// shape, reference #1448; #1430). Cull is the one delivery archetype whose enabler is a genuinely new
        /// damage-calc conditional rather than an existing resistance/amplification attribute: the target's
        /// missing-health fraction at the moment of the hit scales <c>ExecuteBonus</c> into that fire's
        /// multiplier above 1 (the investment), applied to the raw damage identically to <c>CriticalDamage</c>
        /// before mitigation. For each portion the extra damage that investment enabled — the live post-execute
        /// hit minus the same hit without it, both carried through the defender's mitigation — is booked with
        /// the same <c>/(1 + investment)</c> saturation the crit/Hex/Momentum bonuses use, on the investment's
        /// own magnitude, measured off the pre-crit portion so it composes with crit without either overlay
        /// inflating the other. A target at full health enables nothing; a target near death saturates the
        /// multiplier toward the full <c>ExecuteBonus</c>. Backend-only like the other overlay tallies — DoT has
        /// no counterpart (there is no per-tick "hit" to execute).
        /// </summary>
        public double CullBonusDealt { get; set; }

        /// <summary>
        /// The Sunder (<c>Sunder</c> activity key) training signal — a designed proxy for the damage the player's
        /// applied Toughness debuff enabled this battle (spike #1398 → the overlay tally shape, reference
        /// #1448; #1429), booked per hit as <c>dealt × φ(investment)</c> where the investment is the opponent's
        /// applied Toughness reduction (<see cref="Battle.Battler.AppliedSunder"/>) scaled by the curve's own
        /// characteristic magnitude <c>K·attackerLevel</c>. Unlike the other overlays this is <b>not</b> a
        /// literal before/after marginal — the Toughness curve is nonlinear, so (unlike Hex's flat resistance
        /// percentage) there is no way to compute a real marginal through it that is actually independent of the
        /// target's own stats; <c>dealt × φ(investment)</c> is instead a saturating proxy that reads neither the
        /// target's Toughness nor its resistance, so a fixed debuff trains the same regardless of which enemy it
        /// lands on (see <see cref="Battle.Battler.SunderBonusForHit"/>). Direct-hit only: DoT bypasses the
        /// Toughness curve entirely, so a Toughness debuff cannot affect it. Backend-only like the other overlay
        /// tallies.
        /// </summary>
        public double SunderBonusDealt { get; set; }

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

        /// <summary>Accumulates a resistance-blocked <paramref name="amount"/> into the typed resist-mitigated book.</summary>
        public void AddTypedDamageResistanceMitigated(EDamageType type, double amount)
        {
            TypedDamageResistanceMitigated.TryGetValue(type, out var existing);
            TypedDamageResistanceMitigated[type] = existing + amount;
        }

        /// <summary>Accumulates a normalized-marginal vulnerability-enabled <paramref name="amount"/> into the
        /// type-neutral Hex signal (#1427). Cached as a method group by <see cref="BattleContext"/> so the
        /// per-tick DoT phase can record the enemy's Hex bonus without allocating.</summary>
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
