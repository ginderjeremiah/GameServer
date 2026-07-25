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
        /// This battler's timed skill-effect bookkeeping — the per-attribute effect stacks and the backend-only
        /// overlay tallies reading them (#2380). Applying and expiring effects goes through
        /// <see cref="ApplyEffect"/>/<see cref="AdvanceEffects"/> so the MaxHealth re-clamp can't be skipped;
        /// this is exposed for the overlay readers, which never mutate state.
        /// </summary>
        public BattlerEffects Effects { get; }

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
            Effects = new BattlerEffects(attributes);
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
        /// booked (health-capped) damage × <c>φ(v)</c> share claim (<see cref="BattlerEffects.HexBonusForHit"/>; type-neutral, so
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
                    var hexBonus = Effects.HexBonusForHit(bookedTick, accumulators[i].Type);
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
        /// Applies <paramref name="effect"/> as a timed attribute modifier on this battler (see
        /// <see cref="BattlerEffects.Apply"/> for the stacking / shared-expiry rules and the overlay-tracking
        /// flags). A new modifier may shift <see cref="MaxHealth"/>, so the health is re-clamped here — the
        /// reason applying an effect stays on the battler rather than being reached through
        /// <see cref="Effects"/> directly.
        /// </summary>
        public void ApplyEffect(
            SkillEffect effect, double amount,
            bool tracksVulnerability = false, bool tracksMomentum = false, bool tracksSunder = false)
        {
            Effects.Apply(effect, amount, tracksVulnerability, tracksMomentum, tracksSunder);
            ClampHealthToMaxHealth();
        }

        /// <summary>
        /// Advances this battler's simulated-time clock by <paramref name="ms"/> and expires any lapsed effect
        /// stack (see <see cref="BattlerEffects.Advance"/>), re-clamping the health when a removal could have
        /// dropped <see cref="MaxHealth"/>. Called at the start of each tick before any skill fires.
        /// </summary>
        public void AdvanceEffects(int ms)
        {
            if (Effects.Advance(ms))
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
    }
}
