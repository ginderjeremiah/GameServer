using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Classes;
using Game.Core.Skills;
using static Game.Core.EAttribute;

namespace Game.Core.Battle
{
    /// <summary>
    /// Encapsulates a combatant for battle simulation.
    /// </summary>
    public class Battler
    {
        private readonly AttributeCollection _attributes;


        /// <summary>
        /// The active timed skill effects, folded to one <see cref="AttributeEffectStack"/> per affected
        /// attribute rather than one record per application. Every application on an attribute shares a single
        /// expiry, so the stack collapses its magnitudes into a single combined modifier per modifier type
        /// (additive amounts summed, multiplicative factors compounded). This keeps each apply and each
        /// per-tick expiry pass O(affected attributes) no matter how deep a buff stacks — a persistently
        /// re-applied buff that never lapses would otherwise add one modifier per fire and make a battle
        /// O(ticks²). Lazily created so a battler never targeted by an effect allocates nothing, and the expiry
        /// pass stays allocation-free on the replay hot path (#286).
        /// </summary>
        private List<AttributeEffectStack>? _attributeStacks;

        /// <summary>
        /// This battler's elapsed simulated time in ms, advanced one tick at a time by
        /// <see cref="AdvanceEffects"/>. Active effects store an absolute expiry against this clock, so expiry
        /// is a comparison rather than a per-tick countdown and <see cref="ActiveEffect"/> stays immutable.
        /// </summary>
        private long _elapsedMs;

        public double CurrentHealth { get; private set; }

        public List<BattleSkill> Skills { get; private set; }

        public int Level { get; private set; }

        public bool IsDead => CurrentHealth <= 0;

        /// <summary>
        /// The skill this battler ripostes with when it parries an incoming hit (#1457) — the equipped
        /// weapon's signature (the virtual fists' punch bare-handed), resolved once at battler assembly like
        /// the weapon-match gate. <c>null</c> when no counter is resolvable (an unauthored punch, or an enemy
        /// battler — enemies never parry), in which case a parry negates without a riposte.
        /// </summary>
        public Skill? CounterSkill { get; }

        /// <summary>
        /// The class signature passive this battler's <see cref="EAttributeModifierSource.Class"/> modifier was
        /// resolved from, or <c>null</c> for a battler assembled without one (an enemy, or a hand-built test
        /// battler). Carried only so <see cref="CloneWithAttributeDelta"/> can re-resolve an attribute-scaled
        /// passive against the clone's bumped attributes — the live simulation never reads this, since the
        /// modifier is already folded into <paramref name="attributes"/> at construction.
        /// </summary>
        private readonly ClassSignaturePassive? _signaturePassive;

        public Battler(
            AttributeCollection attributes, IEnumerable<Skill> skills, int level, Skill? counterSkill = null,
            ClassSignaturePassive? signaturePassive = null)
        {
            _attributes = attributes;
            CurrentHealth = _attributes[MaxHealth];
            Skills = skills.Select(s => new BattleSkill(s)).ToList();
            Level = level;
            CounterSkill = counterSkill;
            _signaturePassive = signaturePassive;
        }

        public void Update(BattleContext context)
        {
            foreach (var skill in Skills)
            {
                skill.Update(context);
            }
        }

        public double GetCooldownMultiplier()
        {
            // The effective charge rate: the base-1 CooldownRecovery multiplier plus the committed cadence channel
            // CooldownBonus × CooldownBonusMultiplier, the product computed here at consumption like crit/parry/dodge
            // (spike #1426). CooldownBonus idles at 0 (authored-only enabler), so an uncommitted build charges at
            // exactly CooldownRecovery regardless of Agility. See StaticAttributeModifiers for the base/derived formulas.
            return _attributes[CooldownRecovery]
                + _attributes[CooldownBonus] * _attributes[CooldownBonusMultiplier];
        }

        public double GetAttributeValue(EAttribute attribute)
        {
            return _attributes[attribute];
        }

        /// <summary>
        /// Amplifies an outgoing <paramref name="rawDamage"/> hit of the given <paramref name="damageType"/> by
        /// this (attacking) battler's amplification: <c>rawDamage × (1 + Σ applies(type).Amplification)</c>, the
        /// additive sum folded in the fixed <see cref="DamageTypes.AmplificationAttributes"/> order so both
        /// simulators agree bit-for-bit. With no amplification authored the sum is <c>0</c>, so the factor is an
        /// exact <c>1.0</c> and the hit is unchanged (the reduce-to-today identity, spike #1320 Area B).
        /// </summary>
        public double AmplifyDamage(double rawDamage, EDamageType damageType)
        {
            var amplification = 0.0;
            var amplificationAttributes = DamageTypes.AmplificationAttributes(damageType);
            for (var i = 0; i < amplificationAttributes.Count; i++)
            {
                amplification += _attributes[amplificationAttributes[i]];
            }

            return rawDamage * (1 + amplification);
        }

        /// <summary>
        /// The net damage an incoming hit of <paramref name="dealt"/> (already amplified and crit-multiplied) of
        /// the given <paramref name="damageType"/> would deal to this (defending) battler, <b>without</b>
        /// mutating health: percentage resistance first (<c>dealt × (1 − Σ applies(type).Resistance)</c>,
        /// <b>unclamped</b> — a negative total amplifies as vulnerability, a total above <c>1</c> drives the
        /// result negative as absorption), then — only while the post-resistance damage is still positive — the
        /// <see cref="EAttribute.Toughness"/> mitigation multiplier <c>(1 − Toughness / (Toughness + C))</c>
        /// (<c>C</c> = <see cref="GameConstants.ToughnessMitigationConstant"/>). The toughness curve is a
        /// diminishing-returns percentage: effective HP is linear in Toughness while the reduction asymptotes
        /// below <c>100%</c> (no immunity), and the constant denominator means an investment retains its
        /// mitigation % across all of progression (#1487, revising spike #1330's level normalization). The
        /// resistance sum is folded in the fixed <see cref="DamageTypes.ResistanceAttributes"/> order for parity;
        /// with no resistance and no Toughness the positive branch reduces to <c>dealt</c>. The whole stack is
        /// multiplicative — with Block's flat reduction removed (spike #1330 Area B) there is no flat subtraction
        /// left, so the only path to a negative (absorbing) result is a resistance above <c>1</c>, and no clamp
        /// is needed there.
        /// </summary>
        public double ComputeNetDamage(double dealt, EDamageType damageType)
        {
            var mitigated = dealt * (1 - SumTypeResistance(damageType));
            if (mitigated <= 0)
            {
                // Absorption (or a zero hit): the target takes a net heal; the toughness curve does not apply
                // (mitigation can neither heal nor deepen an absorption heal).
                return mitigated;
            }

            // Toughness mitigation: Toughness / (Toughness + C) as a multiplier, so EHP is linear in Toughness
            // and the reduction asymptotes below 100% (a positive hit can never go negative through it). The
            // curve is unclamped below 0 — a debuff-driven negative Toughness amplifies the hit (#1483), with
            // the pole at Toughness = −C left unguarded per #1478 (unreachable by authored content). Both
            // simulators must compute this expression identically for battle parity.
            var toughness = _attributes[Toughness];
            var toughnessReduction = toughness / (toughness + GameConstants.ToughnessMitigationConstant);

            return mitigated * (1 - toughnessReduction);
        }

        // The raw (unclamped, signed) resistance sum for a type — shared by ComputeNetDamage and
        // TypeResistanceMitigated, each applying their own clamping (or none) on top.
        private double SumTypeResistance(EDamageType damageType)
        {
            var resistance = 0.0;
            var resistanceAttributes = DamageTypes.ResistanceAttributes(damageType);
            for (var i = 0; i < resistanceAttributes.Count; i++)
            {
                resistance += _attributes[resistanceAttributes[i]];
            }

            return resistance;
        }

        /// <summary>
        /// The amount of a direct hit of <paramref name="dealt"/> this battler's own type-resistance for
        /// <paramref name="damageType"/> blocks — <c>dealt × clamp(resistance, 0, 1)</c>, deliberately isolated
        /// from the Toughness curve (spike #1398 → resistance training split, #1454). Toughness is a generic,
        /// non-typed stat every build can raise, so folding it in would let it accelerate every resist path's
        /// training at once; this credits only the type-specific resistance investment the path actually
        /// represents. Clamped to <c>[0, 1]</c> because this is a training-weight fraction, not a damage
        /// multiplier: a resistance debuff pushing the sum negative blocks nothing (anti-mitigation, not
        /// credited here), and a sum above <c>1</c> (absorption) still credits at most the full dealt amount.
        /// </summary>
        public double TypeResistanceMitigated(double dealt, EDamageType damageType)
        {
            return dealt * Math.Clamp(SumTypeResistance(damageType), 0, 1);
        }

        /// <summary>
        /// A copy of this battler with <paramref name="delta"/> added to <paramref name="attribute"/> as a
        /// fresh <see cref="EAttributeModifierSource.BaseValue"/> additive term — full cascade re-derivation
        /// included, so bumping a core attribute re-derives everything <see cref="StaticAttributeModifiers"/>
        /// hangs off it exactly like a real allocation would. Used by the combat rating's marginal helper
        /// (<see cref="CombatRating.Marginal"/>, #1531) to price one point of investment via finite difference;
        /// not used by the live battle simulation. Excludes any live <see cref="EAttributeModifierSource.SkillEffect"/>
        /// modifiers (the marginal prices a permanent investment, not an in-battle timed-buff snapshot) and the
        /// static modifiers themselves — copying those too would double them, since the fresh
        /// <see cref="AttributeCollection"/> constructor re-adds them automatically. The frozen
        /// <see cref="EAttributeModifierSource.Class"/> signature-passive modifier is excluded too and, when this
        /// battler carries a <see cref="_signaturePassive"/>, re-resolved against the clone's bumped attributes —
        /// composed last, mirroring <see cref="BattlerMaterials.Build"/> — so an attribute-scaled
        /// passive re-derives exactly like a real allocation would instead of copying through at its
        /// already-resolved (pre-bump) amount (#1862).
        /// </summary>
        public Battler CloneWithAttributeDelta(EAttribute attribute, double delta)
        {
            var staticModifiers = new HashSet<AttributeModifier>(StaticAttributeModifiers.All);
            var modifiers = _attributes.AllModifiers()
                .Where(m => m.Source != EAttributeModifierSource.SkillEffect
                    && m.Source != EAttributeModifierSource.Class
                    && !staticModifiers.Contains(m))
                .ToList();
            modifiers.Add(new AttributeModifier
            {
                Attribute = attribute,
                Amount = delta,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.BaseValue,
            });

            var attributes = new AttributeCollection(modifiers);
            if (_signaturePassive is not null)
            {
                attributes.AddModifier(_signaturePassive.GetModifier(attributes.GetAttributeValue));
            }

            return new Battler(attributes, Skills.Select(s => s.Skill), Level, CounterSkill, _signaturePassive);
        }

        /// <summary>
        /// The shared overlay-tally saturation <c>φ(a) = a / (1 + a)</c>, applied to an overlay's own investment
        /// magnitude when booking its share claim (#1481): ~linear at the low end so a token investment trains
        /// proportionally little, asymptoting to <c>1</c> so even a huge investment claims at most the full booked
        /// hit. Every overlay signal (crit, Hex, Momentum, Sunder, Cull) uses it on its own investment.
        /// </summary>
        public static double NormalizeInvestment(double investment)
        {
            return investment / (1.0 + investment);
        }

        /// <summary>
        /// The Hex bonus for a hit that booked <paramref name="bookedNet"/> (the post-mitigation damage capped at
        /// the health it actually removed, #1482) of <paramref name="damageType"/> against this (defending)
        /// battler — the attacker's Hex signal (#1427), booked as <c>bookedNet × φ(v)</c>
        /// (<see cref="NormalizeInvestment"/>). The vulnerability <c>v</c> is the opponent's own applied resistance
        /// reduction for the type (<see cref="AppliedVulnerability"/>) — tracked as the modifiers the opponent
        /// contributed, so it credits the <b>work the debuff did</b> regardless of the target's base resistance or
        /// its own resistance buffs. A <b>share claim on the damage that actually landed</b>, not a counterfactual
        /// marginal (#1481): the booked nets over a won battle are bounded by this battler's health pool, so the
        /// per-battle claim is ≈ the debuff's coverage share of that pool × <c>φ(v)</c> — enemy-independent at the
        /// accrual level, proportional to the investment through <c>φ</c>, and computed with no counterfactual
        /// curve evaluation. Returns <c>0</c> when no vulnerability is applied. A backend-only side channel — it
        /// never mutates health.
        /// </summary>
        public double HexBonusForHit(double bookedNet, EDamageType damageType)
        {
            var vulnerability = AppliedVulnerability(damageType);
            if (vulnerability <= 0)
            {
                return 0;
            }

            return bookedNet * NormalizeInvestment(vulnerability);
        }

        /// <summary>
        /// The opponent-applied vulnerability on this (defending) battler for <paramref name="damageType"/> — the
        /// total resistance <b>reduction</b> the opponent's timed effects contributed across the type's resistance
        /// attributes, clamped at <c>0</c> (spike #1398 → Hex, #1427). Tracked from the effects the opponent
        /// applied (<see cref="ApplyEffect"/>'s <c>tracksVulnerability</c>) rather than diffed against a baseline,
        /// so it credits the debuff's own work even when the target's base resistance is high or the target buffs
        /// its own resistance (a self-buff never lowers this). Rides the shared per-attribute effect-stack expiry,
        /// so it returns to <c>0</c> for free when the debuff lapses.
        /// </summary>
        public double AppliedVulnerability(EDamageType damageType)
        {
            if (_attributeStacks is null)
            {
                return 0;
            }

            var contribution = 0.0;
            var resistanceAttributes = DamageTypes.ResistanceAttributes(damageType);
            for (var i = 0; i < resistanceAttributes.Count; i++)
            {
                contribution += StackContribution(resistanceAttributes[i], stack => stack.VulnerabilityContribution);
            }

            // Contributions are the signed resistance deltas the opponent applied (negative for a debuff); the
            // vulnerability is their reduction, so negate and clamp — an opponent that only raised resistance is 0.
            return contribution < 0 ? -contribution : 0;
        }

        /// <summary>
        /// The Sunder bonus for a hit that booked <paramref name="bookedNet"/> (the post-mitigation damage capped
        /// at the health it actually removed, #1482) against this (defending) battler — the attacker's Sunder
        /// signal (#1429), booked as <c>bookedNet × φ(investment)</c> (<see cref="NormalizeInvestment"/>), where
        /// the investment is the opponent-applied Toughness reduction (<see cref="AppliedSunder"/>) made
        /// dimensionless by the curve's own characteristic magnitude
        /// (<see cref="GameConstants.ToughnessMitigationConstant"/>) — the same constant the live mitigation curve
        /// divides by. The same share-claim shape as every overlay tally (#1481; Sunder pioneered the
        /// no-counterfactual proxy because the nonlinear Toughness curve has no target-flat marginal). Returns
        /// <c>0</c> when no Sunder debuff is applied. A backend-only side channel — it never mutates health.
        /// Direct-hit only: DoT bypasses the Toughness curve entirely (a Toughness debuff cannot affect it), so
        /// there is no DoT counterpart to this method.
        /// </summary>
        public double SunderBonusForHit(double bookedNet)
        {
            var sunder = AppliedSunder();
            if (sunder <= 0)
            {
                return 0;
            }

            return bookedNet * NormalizeInvestment(sunder / GameConstants.ToughnessMitigationConstant);
        }

        /// <summary>
        /// The opponent-applied Toughness reduction on this (defending) battler — the total <see cref="Toughness"/>
        /// debuff the opponent's timed effects contributed, clamped at <c>0</c> (spike #1398 → Sunder, #1429).
        /// Tracked from the effects the opponent applied (<see cref="ApplyEffect"/>'s <c>tracksSunder</c>) rather
        /// than diffed against a baseline, so it credits the debuff's own work even when the target's base
        /// Toughness is high or the target buffs its own Toughness (a self-buff never lowers this). Toughness is
        /// untyped and a single attribute (unlike resistance's per-type keys), so this reads one stack directly
        /// rather than folding over a type's resistance attributes. Rides the shared per-attribute effect-stack
        /// expiry, so it returns to <c>0</c> for free when the debuff lapses.
        /// </summary>
        public double AppliedSunder()
        {
            var contribution = StackContribution(Toughness, stack => stack.SunderContribution);
            return contribution < 0 ? -contribution : 0;
        }

        /// <summary>
        /// This (attacking) battler's own applied ramp on <paramref name="damageType"/> — the total amplification
        /// its <b>own</b> timed self-buffs contributed across the type's amplification attributes (spike #1398 →
        /// Momentum, #1428). Tracked from the effects this battler applied to itself
        /// (<see cref="ApplyEffect"/>'s <c>tracksMomentum</c>), so it isolates the ramp's own contribution from
        /// any static (item/base) amplification the battler already carries. Rides the shared per-attribute
        /// effect-stack expiry, so it returns to <c>0</c> for free when the ramp lapses.
        /// </summary>
        public double AppliedMomentum(EDamageType damageType)
        {
            if (_attributeStacks is null)
            {
                return 0;
            }

            var contribution = 0.0;
            var amplificationAttributes = DamageTypes.AmplificationAttributes(damageType);
            for (var i = 0; i < amplificationAttributes.Count; i++)
            {
                contribution += StackContribution(amplificationAttributes[i], stack => stack.MomentumContribution);
            }

            return contribution > 0 ? contribution : 0;
        }

        // The tracked contribution (selected via <paramref name="selector"/>) an active effect stack has
        // accrued for one attribute, or 0 when no stack for it exists yet. A linear scan over the
        // affected-attribute count, like GetOrCreateStack. Shared by the per-overlay applied-* readers
        // (AppliedVulnerability, AppliedMomentum, ...) so each just supplies its own contribution field.
        private double StackContribution(EAttribute attribute, Func<AttributeEffectStack, double> selector)
        {
            if (_attributeStacks is null)
            {
                return 0;
            }

            foreach (var stack in _attributeStacks)
            {
                if (stack.Attribute == attribute)
                {
                    return selector(stack);
                }
            }

            return 0;
        }

        /// <summary>
        /// Applies an incoming hit of <paramref name="dealt"/> (already amplified and crit-multiplied) of the
        /// given <paramref name="damageType"/> via <see cref="ComputeNetDamage"/> — percentage resistance then
        /// the <see cref="EAttribute.Toughness"/> mitigation curve.
        /// Returns the net damage dealt; a negative result (absorption) heals this battler, <b>capped at
        /// <see cref="MaxHealth"/></b> — the game has no overheal/shield concept, so this matches
        /// <see cref="ApplyHealOverTime"/> rather than letting the reactive absorption channel bank health above
        /// the cap.
        /// </summary>
        public double TakeDamage(double dealt, EDamageType damageType)
        {
            var net = ComputeNetDamage(dealt, damageType);
            if (net < 0)
            {
                // Absorption: cap the heal at the remaining room to MaxHealth, and report the actual healed
                // amount so the per-skill / global stats stay reconciled.
                var heal = CapHealToRoom(-net);
                CurrentHealth += heal;
                return heal == 0 ? 0 : -heal;
            }

            CurrentHealth -= net;
            return net;
        }

        /// <summary>
        /// Caps <paramref name="heal"/> to this battler's remaining room to <see cref="MaxHealth"/>, floored at
        /// <c>0</c> — never negative, and never a negative zero when the room is fully exhausted. Shared by the
        /// three channels whose net effect can be a heal — <see cref="TakeDamage"/>'s direct-hit absorption,
        /// <see cref="ApplyDamageOverTime"/>'s aggregate DoT-absorption, and <see cref="ApplyHealOverTime"/> —
        /// since the game has no overheal/shield concept regardless of the heal's source.
        /// </summary>
        private double CapHealToRoom(double heal)
        {
            var room = _attributes[MaxHealth] - CurrentHealth;
            var capped = heal < room ? heal : room;
            return capped > 0 ? capped : 0;
        }

        /// <summary>
        /// The share of a hit's <paramref name="damage"/> that removed live health, given the target's
        /// <paramref name="healthBefore"/> it landed: capped at the positive health remaining, so a killing
        /// blow's overkill tail books nothing (#1482), and floored at 0, so a portion that instead healed the
        /// target under authored absorption (resistance &gt; 1) books nothing rather than going negative and
        /// offsetting a sibling type's genuine offense-book training (#2101). A booking rule for the
        /// proficiency offense book only — the health math (both direct hits and DoT ticks) is never capped or
        /// floored.
        /// </summary>
        public static double HealthRemoved(double damage, double healthBefore)
        {
            return Math.Clamp(damage, 0, Math.Max(0, healthBefore));
        }

        /// <summary>
        /// Subtracts <paramref name="amount"/> of reflected damage directly from this (attacking) battler's
        /// health, <b>bypassing all of its own mitigation</b> (resistance and the Toughness curve) — the
        /// deterministic damage-reflection channel (spike #1330). The caller resolves the amount
        /// (defender net × the defender's <see cref="EAttribute.DamageReflection"/>) and reflects only a
        /// positive hit, so this is a raw health subtraction with no floor or cap.
        /// </summary>
        public void TakeReflectedDamage(double amount)
        {
            CurrentHealth -= amount;
        }

        /// <summary>
        /// Applies one tick of typed damage-over-time (spike #1320, Area C). Loops the DoT types in the fixed
        /// <see cref="DamageTypes.DotAccumulators"/> order, scaling each type's per-second accumulator to
        /// <paramref name="ms"/> and applying this (defending) battler's resistance for that type <b>sampled
        /// live</b> — <c>perSec × ms/1000 × (1 − Σ applies(type).Resistance)</c> — so a vulnerability debuff
        /// makes existing DoTs hurt immediately. The caster's amplification was already frozen into the
        /// accumulator at apply time (<see cref="BattleContext.ApplySkillEffect"/>). Unlike
        /// <see cref="TakeDamage"/> it <b>bypasses the Toughness curve</b> — resistance is its only mitigation —
        /// and is never reflected (reflection is scoped to direct hits, spike #1330); it returns the total damage
        /// dealt so the caller can attribute it to the battle statistics. With no DoT authored every accumulator
        /// is <c>0</c>, so the loop adds nothing and the return is an exact <c>0</c>.
        /// </summary>
        /// <remarks>
        /// Each type's own tick is intentionally <b>not</b> floored at zero. DoT bypasses mitigation entirely, so
        /// a tick goes negative only through a deliberately authored negative accumulator or a resistance above
        /// <c>1</c> (absorption) — and a floor wouldn't prevent that, it would just silently rewrite the value;
        /// the per-type recorders above always see this uncapped tick. But the <b>aggregate</b> health change
        /// this call realizes <i>is</i> capped at the remaining room to <see cref="MaxHealth"/> when the summed
        /// <c>dot</c> is negative — matching <see cref="TakeDamage"/>'s absorption cap and
        /// <see cref="ApplyHealOverTime"/> (the game has no overheal/shield concept). The resistance
        /// sum is folded in the fixed <see cref="DamageTypes.ResistanceAttributes"/> order, and each type's
        /// contribution is summed in <see cref="DamageTypes.DotAccumulators"/> order, so both simulators agree
        /// bit-for-bit. A single typed DoT with no resistance reduces to <c>perSec × ms/1000</c> — byte-identical
        /// to the former single-accumulator outcome (the reduce-to-today identity).
        /// </remarks>
        /// <param name="ms">The elapsed simulated time this tick.</param>
        /// <param name="recordExposure">
        /// Optional per-type <b>pre-mitigation</b> recorder for the proficiency incoming book (spike #1337) —
        /// invoked with each DoT type and its tick damage <em>before</em> this battler's resistance. Supplied
        /// only when this battler's exposure is tracked (the player); <c>null</c> leaves the loop unchanged. It
        /// is a backend-only side channel that never touches the health math, so it adds no parity surface.
        /// </param>
        /// <param name="recordDamageDealt">
        /// Optional per-type <b>post-mitigation</b> recorder for the proficiency offense book (spike #1338) —
        /// invoked with each DoT type and the tick damage <em>after</em> this battler's resistance, capped at
        /// the health the tick actually removes and floored at 0 (<see cref="HealthRemoved"/>, #1482/#2101)
        /// across the fixed accumulator order, so a killing tick's overkill tail — and a tick that instead
        /// healed this battler under authored absorption — both book nothing, while the health math below
        /// stays uncapped. Supplied when this battler is the victim of the player's DoT (the enemy), so the
        /// player's DoT damage dealt is typed for the offense binding consistently with a direct hit's booked damage;
        /// <c>null</c> leaves the loop unchanged. Like <paramref name="recordExposure"/> it is a backend-only
        /// side channel that adds no parity surface.
        /// </param>
        /// <param name="recordHexBonus">
        /// Optional recorder for the attacker's Hex signal (#1427/#1481) — invoked per DoT type with the tick's
        /// booked (health-capped) damage × <c>φ(v)</c> share claim (<see cref="HexBonusForHit"/>; type-neutral, so
        /// it takes just the amount). Supplied only when this battler is the victim of the player's DoT (the
        /// enemy); <c>null</c> skips the vulnerability lookup entirely. A backend-only side channel like the others.
        /// </param>
        /// <param name="recordMitigated">
        /// Optional per-type recorder for the resist-mitigated portion of the resist-training split (#1454) —
        /// invoked with each DoT type and <c>preMitigation × clamp(resistance, 0, 1)</c>, the amount this
        /// battler's own type-resistance blocked. DoT bypasses the Toughness curve entirely (resistance is its
        /// only mitigation), so unlike the direct-hit path this needs no separate Toughness-excluding helper.
        /// Supplied only when this battler's exposure is tracked (the player); <c>null</c> leaves the loop
        /// unchanged. A backend-only side channel like the others.
        /// </param>
        public double ApplyDamageOverTime(
            int ms,
            Action<EDamageType, double>? recordExposure = null,
            Action<EDamageType, double>? recordDamageDealt = null,
            Action<double>? recordHexBonus = null,
            Action<EDamageType, double>? recordMitigated = null)
        {
            var dot = 0.0;
            // The positive health the offense book can still credit this tick (#1482): damage-dealt booking is
            // capped at the health each type actually removes, tracked through the fixed accumulator order (a
            // negative tick — an authored absorption heal — restores it). Booking only; the health math is untouched.
            var bookableHealth = Math.Max(0, CurrentHealth);
            var accumulators = DamageTypes.DotAccumulators;
            for (var i = 0; i < accumulators.Count; i++)
            {
                var perSecond = _attributes[accumulators[i].Accumulator];
                if (perSecond == 0)
                {
                    continue;
                }

                // The pre-mitigation tick (before this battler's resistance) is its exposure to this DoT type;
                // record it for the incoming book when a recorder is supplied. Folded out of the dot sum below
                // so the recorded value and the mitigated value share one multiplication (no parity drift).
                var preMitigation = perSecond * ms / 1000.0;
                recordExposure?.Invoke(accumulators[i].Type, preMitigation);

                var resistance = 0.0;
                var resistanceAttributes = DamageTypes.ResistanceAttributes(accumulators[i].Type);
                for (var j = 0; j < resistanceAttributes.Count; j++)
                {
                    resistance += _attributes[resistanceAttributes[j]];
                }

                // The post-resistance tick is both what the health loses and what the attacker dealt of this
                // type; compute it once so the recorded damage-dealt and the health math cannot drift. The
                // booked amount is capped at the health the tick actually removes (#1482).
                var tickDamage = preMitigation * (1 - resistance);
                var bookedTick = HealthRemoved(tickDamage, bookableHealth);
                bookableHealth = Math.Max(0, bookableHealth - tickDamage);
                recordDamageDealt?.Invoke(accumulators[i].Type, bookedTick);
                recordMitigated?.Invoke(accumulators[i].Type, preMitigation * Math.Clamp(resistance, 0, 1));

                // The attacker's Hex bonus for this tick (#1427/#1481): the same share claim the direct-hit tally
                // books — the tick's booked (health-capped) damage × φ(v) — so direct hits and DoT ticks share one
                // shape. An absorbed or fully-overkilled tick (booked ≤ 0) trains nothing.
                if (recordHexBonus is not null && bookedTick > 0)
                {
                    var hexBonus = HexBonusForHit(bookedTick, accumulators[i].Type);
                    if (hexBonus > 0)
                    {
                        recordHexBonus(hexBonus);
                    }
                }

                dot += tickDamage;
            }

            if (dot < 0)
            {
                // Aggregate absorption (net heal across the ticked types): cap the realized health change at
                // the remaining room to MaxHealth, consistent with TakeDamage's absorption cap and
                // ApplyHealOverTime — the game has no overheal/shield concept. Per-type booking above already
                // recorded the uncapped tick.
                var heal = CapHealToRoom(-dot);
                dot = heal == 0 ? 0 : -heal; // avoid -0.0, matching the frontend mirror bit-for-bit
            }

            CurrentHealth -= dot;
            return dot;
        }

        /// <summary>
        /// Applies one tick of heal-over-time from <see cref="HealthRegenPerSecond"/> (authored per second,
        /// scaled to <paramref name="ms"/>), capped at <see cref="MaxHealth"/>. Returns the actual (post-cap)
        /// health restored so the caller can attribute it to the battle statistics.
        /// </summary>
        public double ApplyHealOverTime(int ms)
        {
            var heal = _attributes[HealthRegenPerSecond] * ms / 1000.0;
            var healed = CapHealToRoom(heal);
            if (healed > 0)
            {
                CurrentHealth += healed;
                return healed;
            }

            return 0;
        }

        /// <summary>
        /// Applies <paramref name="effect"/> as a timed attribute modifier on this battler, using the
        /// already-resolved <paramref name="amount"/> as its magnitude (the caster's attribute scaling is
        /// applied by <see cref="BattleContext.ApplySkillEffect"/> before this is reached). Each application
        /// <b>stacks</b>: its magnitude folds into the attribute's single combined modifier for the effect's
        /// type (additive amounts add, multiplicative factors compound). All active applications targeting the
        /// <b>same attribute</b> share a single expiry: applying any effect on that attribute resets the whole
        /// stack to this application's duration, so it expires together with no independent per-portion
        /// expirations (#992 / #740). A new modifier may shift <see cref="MaxHealth"/>, so the health is
        /// re-clamped.
        /// <para>
        /// When <paramref name="tracksVulnerability"/> is set (an opponent-applied resistance debuff — the Hex
        /// enabler, #1427), the resolved additive <paramref name="amount"/> is also accumulated onto the stack's
        /// <see cref="AttributeEffectStack.VulnerabilityContribution"/>, so <see cref="AppliedVulnerability"/> can
        /// credit the debuff's own work independently of the target's base resistance or its own buffs. It rides
        /// the shared stack expiry — cleared for free when the stack lapses — and is a backend-only side channel
        /// that never touches the combined modifier or the health math, so it adds no parity surface.
        /// </para>
        /// <para>
        /// When <paramref name="tracksMomentum"/> is set (a self-applied amplification ramp — the Momentum
        /// enabler, #1428), <paramref name="amount"/> is likewise accumulated onto the stack's
        /// <see cref="AttributeEffectStack.MomentumContribution"/>, so <see cref="AppliedMomentum"/> can isolate
        /// the ramp's own contribution from any static amplification the battler already carries. Same shared
        /// expiry, same no-parity-surface side channel as the vulnerability tracker above.
        /// </para>
        /// <para>
        /// When <paramref name="tracksSunder"/> is set (an opponent-applied Toughness debuff — the Sunder
        /// enabler, #1429), <paramref name="amount"/> is likewise accumulated onto the stack's
        /// <see cref="AttributeEffectStack.SunderContribution"/>, so <see cref="AppliedSunder"/> can credit the
        /// debuff's own work independently of the target's base Toughness or its own buffs. Same shared expiry,
        /// same no-parity-surface side channel as the trackers above.
        /// </para>
        /// </summary>
        public void ApplyEffect(
            SkillEffect effect, double amount,
            bool tracksVulnerability = false, bool tracksMomentum = false, bool tracksSunder = false)
        {
            var stack = GetOrCreateStack(effect.AttributeId);

            // Re-applying any effect on this attribute resets the whole stack's shared expiry to the new
            // application's duration (it may extend a longer-lived application or cut a shorter one short).
            stack.ExpiresAtMs = _elapsedMs + effect.DurationMs;

            // An opponent-applied resistance debuff also accrues its signed delta to the vulnerability tally the
            // Hex signal reads — separate from the combined modifier below, so the health math is untouched.
            if (tracksVulnerability)
            {
                stack.VulnerabilityContribution += amount;
            }

            // A self-applied amplification ramp likewise accrues its delta to the Momentum tally's contribution
            // tracker — separate from the combined modifier below, so the health math is untouched.
            if (tracksMomentum)
            {
                stack.MomentumContribution += amount;
            }

            // An opponent-applied Toughness debuff likewise accrues its signed delta to the Sunder tally's
            // contribution tracker — separate from the combined modifier below, so the health math is untouched.
            if (tracksSunder)
            {
                stack.SunderContribution += amount;
            }

            // Fold the application into the attribute's single combined modifier for its type, swapping the old
            // combined instance for the new one. The collection therefore holds at most one effect modifier per
            // (attribute, type) regardless of how deep the stack runs.
            if (effect.ModifierType is EModifierType.Multiplicative)
            {
                var combined = (stack.Multiplicative?.Amount ?? 1.0) * amount;
                stack.Multiplicative = SwapCombinedModifier(
                    stack.Multiplicative, effect.AttributeId, EModifierType.Multiplicative, combined);
            }
            else
            {
                var combined = (stack.Additive?.Amount ?? 0.0) + amount;
                stack.Additive = SwapCombinedModifier(
                    stack.Additive, effect.AttributeId, EModifierType.Additive, combined);
            }

            ClampHealthToMaxHealth();
        }

        /// <summary>
        /// Advances this battler's simulated-time clock by <paramref name="ms"/> and removes any attribute
        /// stack whose shared expiry has been reached (its combined modifiers removed and health re-clamped).
        /// Called at the start of each tick before any skill fires, so an effect influences exactly
        /// <c>DurationMs / tickSize</c> ticks counting the one it was applied on.
        /// </summary>
        public void AdvanceEffects(int ms)
        {
            // Advance the clock every tick, even with no active effects, so an effect applied on a later tick
            // still computes its absolute expiry from the correct elapsed time.
            _elapsedMs += ms;

            if (_attributeStacks is null || _attributeStacks.Count == 0)
            {
                return;
            }

            var removedAny = false;
            for (var i = _attributeStacks.Count - 1; i >= 0; i--)
            {
                var stack = _attributeStacks[i];
                if (stack.ExpiresAtMs <= _elapsedMs)
                {
                    if (stack.Additive is not null)
                    {
                        _attributes.RemoveModifier(stack.Additive);
                    }

                    if (stack.Multiplicative is not null)
                    {
                        _attributes.RemoveModifier(stack.Multiplicative);
                    }

                    _attributeStacks.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                ClampHealthToMaxHealth();
            }
        }

        /// <summary>
        /// Clamps <see cref="CurrentHealth"/> down to <see cref="MaxHealth"/> when an effect change has dropped
        /// the maximum below it; a rise in MaxHealth leaves CurrentHealth untouched (no free healing).
        /// </summary>
        private void ClampHealthToMaxHealth()
        {
            var maxHealth = _attributes[MaxHealth];
            if (CurrentHealth > maxHealth)
            {
                CurrentHealth = maxHealth;
            }
        }

        // Returns the stack for the given attribute, creating it (and the backing list) on first use. The scan
        // is over the affected-attribute count (≤ the attribute count), never the application count, so it
        // stays cheap.
        private AttributeEffectStack GetOrCreateStack(EAttribute attribute)
        {
            _attributeStacks ??= [];
            foreach (var stack in _attributeStacks)
            {
                if (stack.Attribute == attribute)
                {
                    return stack;
                }
            }

            var created = new AttributeEffectStack(attribute);
            _attributeStacks.Add(created);
            return created;
        }

        // Removes the previous combined modifier (if any) and adds the new one, returning it to be stored back
        // on the stack. AttributeModifier is immutable, so the running magnitude is carried by swapping whole
        // instances — keeping a single effect modifier per (attribute, type) in the collection.
        private AttributeModifier SwapCombinedModifier(
            AttributeModifier? existing, EAttribute attribute, EModifierType type, double amount)
        {
            if (existing is not null)
            {
                _attributes.RemoveModifier(existing);
            }

            var modifier = new AttributeModifier
            {
                Attribute = attribute,
                Amount = amount,
                Type = type,
                Source = EAttributeModifierSource.SkillEffect,
            };
            _attributes.AddModifier(modifier);
            return modifier;
        }

        /// <summary>
        /// The folded active-effect state for one attribute: the absolute simulated time (in
        /// <see cref="_elapsedMs"/> ms) at which the whole stack expires — shared by every application on the
        /// attribute and reset to the newest application's duration (see <see cref="ApplyEffect"/>) — plus the
        /// single combined modifier currently in the collection for each modifier type (null when no
        /// application of that type is active). Folding the applications keeps the per-tick expiry pass and
        /// per-fire application bounded by the affected-attribute count rather than the unbounded stack depth.
        /// </summary>
        private sealed class AttributeEffectStack(EAttribute attribute)
        {
            public EAttribute Attribute { get; } = attribute;
            public long ExpiresAtMs { get; set; }
            public AttributeModifier? Additive { get; set; }
            public AttributeModifier? Multiplicative { get; set; }

            /// <summary>
            /// The signed resistance delta an opponent's tracked debuffs contributed to this attribute (negative
            /// for a vulnerability), summed across applications and read by <see cref="AppliedVulnerability"/> for
            /// the Hex tally (#1427). Separate from the combined modifiers above so it never touches the health
            /// math, and cleared with the stack on the shared expiry. <c>0</c> for any non-debuffed attribute.
            /// </summary>
            public double VulnerabilityContribution { get; set; }

            /// <summary>
            /// The amplification this battler's own tracked ramp applications contributed to this attribute,
            /// summed across applications and read by <see cref="AppliedMomentum"/> for the Momentum tally
            /// (#1428). Separate from the combined modifiers above so it never touches the health math, and
            /// cleared with the stack on the shared expiry. <c>0</c> for any non-ramped attribute.
            /// </summary>
            public double MomentumContribution { get; set; }

            /// <summary>
            /// The signed Toughness delta an opponent's tracked debuffs contributed to this attribute (negative
            /// for a Sunder debuff), summed across applications and read by <see cref="AppliedSunder"/> for the
            /// Sunder tally (#1429). Separate from the combined modifiers above so it never touches the health
            /// math, and cleared with the stack on the shared expiry. Only ever populated on the Toughness stack.
            /// </summary>
            public double SunderContribution { get; set; }
        }
    }
}
