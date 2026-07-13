using Game.Core.Attributes;
using Game.Core.Skills;
using static Game.Core.EAttribute;

namespace Game.Core.Battle
{
    public class BattleContext
    {
        private readonly Battler _playerBattler;
        private readonly Battler _enemyBattler;
        private readonly Mulberry32 _rng;
        private Battler _activeBattler;
        private Battler _targetBattler;
        private bool _isPlayerActive;

        // The player's typed-exposure recorder (incoming book), the player's typed resist-mitigated recorder
        // (the resist-training split, #1454), and the player's typed DoT-damage-dealt recorder (offense book),
        // each cached once — a method group converts to a fresh delegate on every use — so the per-tick DoT
        // phase can hand them to ApplyDamageOverTime without allocating on the replay hot path.
        private readonly Action<EDamageType, double> _recordPlayerExposure;
        private readonly Action<EDamageType, double> _recordPlayerMitigated;
        private readonly Action<EDamageType, double> _recordPlayerDotDealt;

        // The player's Hex-signal recorder for the enemy's DoT phase, cached once so the per-tick DoT loop hands
        // the enemy its vulnerability share claim without allocating on the replay hot path (#1427/#1481).
        private readonly Action<double> _recordPlayerHexBonus;

        public int TimeDelta { get; set; }
        public BattleStats Stats { get; } = new();

        public BattleContext(Battler playerBattler, Battler enemyBattler, int timeDelta, Mulberry32 rng)
        {
            _playerBattler = playerBattler;
            _enemyBattler = enemyBattler;
            _rng = rng;
            _activeBattler = playerBattler;
            _targetBattler = enemyBattler;
            _isPlayerActive = true;
            TimeDelta = timeDelta;
            _recordPlayerExposure = Stats.AddTypedDamageExposure;
            _recordPlayerMitigated = Stats.AddTypedDamageResistanceMitigated;
            _recordPlayerDotDealt = Stats.AddTypedDamageDealt;
            _recordPlayerHexBonus = Stats.AddHexBonus;
        }

        public void SwapActiveAndTargetBattlers()
        {
            (_targetBattler, _activeBattler) = (_activeBattler, _targetBattler);
            _isPlayerActive = !_isPlayerActive;
        }

        /// <summary>
        /// The battler currently acting (attacking / casting). Exposed for the damage arithmetic that reads
        /// the caster's attributes (<see cref="BattleSkill.CalculateRawDamage"/>).
        /// </summary>
        public Battler ActiveBattler => _activeBattler;

        public double GetActiveBattlerCooldownMultiplier()
        {
            return _activeBattler.GetCooldownMultiplier();
        }

        /// <summary>
        /// Applies a skill <paramref name="effect"/> to the battler its <see cref="ESkillEffectTarget"/>
        /// selects: <see cref="ESkillEffectTarget.Self"/> to the active (casting) battler,
        /// <see cref="ESkillEffectTarget.Opponent"/> to the target battler. The effect's magnitude scales
        /// off the <b>caster</b> (active battler) — <c>Amount + casterAttribute × ScalingAmount</c>, mirroring
        /// how a <see cref="Skills.DamageMultiplier"/> scales skill damage off the caster — so a
        /// <c>ScalingAmount</c> of <c>0</c> leaves the authored amount unchanged. When the effect targets a DoT
        /// per-second accumulator, the caster's typed amplification is then <b>frozen</b> into the magnitude at
        /// apply time (spike #1320, Area C) — consistent with how the caster scaling already freezes — so the
        /// accumulated DoT carries the caster's amplification while the defender's resistance is sampled live
        /// each tick by <see cref="Battler.ApplyDamageOverTime"/>.
        /// </summary>
        public void ApplySkillEffect(SkillEffect effect)
        {
            var amount = effect.Amount + _activeBattler.GetAttributeValue(effect.ScalingAttributeId) * effect.ScalingAmount;
            if (DamageTypes.DotTypeForAccumulator(effect.AttributeId) is EDamageType dotType)
            {
                amount = _activeBattler.AmplifyDamage(amount, dotType);
            }

            // An opponent-targeted additive resistance debuff is the Hex vulnerability enabler (#1427): track its
            // signed delta on the target so the attacker's Hex signal credits the resistance its debuff removed,
            // independently of the target's base resistance or its own resistance buffs.
            var tracksVulnerability = effect.Target is ESkillEffectTarget.Opponent
                && effect.ModifierType is EModifierType.Additive
                && DamageTypes.IsResistanceAttribute(effect.AttributeId);

            // A self-targeted additive amplification buff is the Momentum ramp enabler (#1428): track its delta
            // on the caster's own stack so the Momentum signal credits only the ramp's own contribution,
            // isolated from any static amplification the caster already carries.
            var tracksMomentum = effect.Target is ESkillEffectTarget.Self
                && effect.ModifierType is EModifierType.Additive
                && DamageTypes.IsAmplificationAttribute(effect.AttributeId);

            // An opponent-targeted additive Toughness debuff is the Sunder enabler (#1429): track its signed
            // delta on the target so the attacker's Sunder signal credits the mitigation its debuff removed.
            var tracksSunder = effect.Target is ESkillEffectTarget.Opponent
                && effect.ModifierType is EModifierType.Additive
                && effect.AttributeId is Toughness;

            var battler = effect.Target is ESkillEffectTarget.Self ? _activeBattler : _targetBattler;
            battler.ApplyEffect(effect, amount, tracksVulnerability, tracksMomentum, tracksSunder);
        }

        /// <summary>
        /// Resolves the end-of-tick damage/heal-over-time phase for both battlers, recording its statistics.
        /// Called only when both battlers are still alive after the skill exchange. For each battler its typed
        /// damage-over-time (<see cref="Battler.ApplyDamageOverTime"/>, bypassing mitigation) is applied, then its
        /// <see cref="EAttribute.HealthRegenPerSecond"/>, and only <b>then</b> is death checked — so a
        /// heal-over-time can save a battler from an otherwise-lethal DoT tick (#1090). The <b>enemy resolves
        /// first</b>: an enemy that the same-tick regen cannot save dies and the phase returns before the
        /// player's DoT applies, so a same-tick mutual DoT kill leaves the player alive (ties favour the
        /// player, consistent with the skill-exchange order). The caller awards victory/loss from the
        /// battlers' resulting <see cref="Battler.IsDead"/> state. DoT on the enemy counts toward
        /// <see cref="BattleStats.PlayerDamageDealt"/>, DoT on the player toward
        /// <see cref="BattleStats.PlayerDamageTaken"/>, and the player's post-cap healing toward
        /// <see cref="BattleStats.PlayerDamageHealed"/>.
        /// </summary>
        public void ResolveDamageOverTime()
        {
            // The enemy's DoT is the player's DoT damage dealt: record its post-mitigation per-type amount into
            // the typed offense book (#1338) — not as exposure, which is the player's incoming concern. The
            // player's own incoming DoT records its pre-mitigation exposure into the incoming book instead.
            Stats.PlayerDamageDealt += _enemyBattler.ApplyDamageOverTime(
                TimeDelta, recordDamageDealt: _recordPlayerDotDealt, recordHexBonus: _recordPlayerHexBonus);
            _enemyBattler.ApplyHealOverTime(TimeDelta);
            if (_enemyBattler.IsDead)
            {
                return;
            }

            Stats.PlayerDamageTaken += _playerBattler.ApplyDamageOverTime(
                TimeDelta, recordExposure: _recordPlayerExposure, recordMitigated: _recordPlayerMitigated);
            Stats.PlayerDamageHealed += _playerBattler.ApplyHealOverTime(TimeDelta);
        }

        /// <summary>
        /// Deals one skill hit's <paramref name="rawDamage"/> to the current target by <b>splitting it across the
        /// skill's weighted <paramref name="portions"/></b> (spike #1343): each portion takes
        /// <c>raw × weight ÷ Σweights</c> of the single raw hit (the weight total folded in fixed portion order, a
        /// parity contract — float addition is not associative) and runs the existing single-type pipeline
        /// (<see cref="Battler.AmplifyDamage"/> → crit → <see cref="Battler.TakeDamage"/>) under its own
        /// <see cref="EDamageType"/>; the per-portion nets are summed into <c>totalNet</c>. A <b>single</b>
        /// portion reduces byte-for-byte to the pre-feature single-typed hit (<c>raw × w ÷ w = raw</c>).
        /// <para>
        /// The seeded RNG is drawn a <b>fixed number of times per fire regardless of portion count or roll
        /// outcome</b>, so the per-tick draw count stays <c>playerFires×1 + enemyFires×3</c>:
        /// </para>
        /// <list type="bullet">
        /// <item>When the <b>player</b> attacks, a single crit draw is taken (always, before damage) against
        /// <paramref name="baseCriticalChance"/> — the firing skill's own opt-in enabler (#1453) — scaled by the
        /// active battler's <see cref="EAttribute.CriticalChanceMultiplier"/>. A crit multiplies <b>every</b>
        /// portion by <see cref="EAttribute.CriticalDamage"/> (read directly) <b>before</b> mitigation —
        /// identical to scaling the whole hit — so high crit damage punches through
        /// <see cref="EAttribute.Toughness"/>.</item>
        /// <item>When the <b>enemy</b> attacks the player, <b>three</b> draws are taken unconditionally, in
        /// fixed order: a parry draw (#1457, against <see cref="EAttribute.ParryChance"/> ×
        /// <see cref="EAttribute.ParryChanceMultiplier"/>), a dodge draw (#1523, against
        /// <see cref="EAttribute.DodgeChance"/> × <see cref="EAttribute.DodgeChanceMultiplier"/>; Block's former
        /// second draw was retired in spike #1330), and the parry counter's crit draw — consumed even when no
        /// parry procs or the defender has no counter skill, so the stream stays a pure function of the fire
        /// sequence. A parry takes precedence over a dodge; either zeroes the <b>whole</b> multi-typed hit, and
        /// a parry additionally fires the defender's riposte (see <see cref="FireParryCounter"/>).</item>
        /// </list>
        /// The per-portion typed books (<see cref="BattleStats.AddTypedDamageDealt"/> per portion's net capped at
        /// the health it actually removed — overkill books nothing, #1482 —
        /// <see cref="BattleStats.AddTypedDamageExposure"/> per portion's pre-resist dealt) are the cross-school
        /// proficiency signal; the whole-hit stats (<see cref="BattleStats.HighestPlayerAttack"/>,
        /// <see cref="BattleStats.CriticalDamageDealt"/>, and the per-skill total via the return) use
        /// <c>totalNet</c> — one swing is one attack, and it deliberately keeps the full uncapped net. Every
        /// overlay training signal books the <b>same share-claim shape</b> (#1481): the portion's booked
        /// (health-capped) net × <c>φ</c> of the overlay's own investment
        /// (<see cref="Battler.NormalizeInvestment"/>) — Precision
        /// (<see cref="BattleStats.CriticalBonusDealt"/>, #1448, on the whole crit hit's booked total with the
        /// crit-damage investment <c>m − 1</c>), Hex (<see cref="BattleStats.HexBonusDealt"/>, #1427, on the
        /// opponent-applied vulnerability), Sunder (<see cref="BattleStats.SunderBonusDealt"/>, #1429, on the
        /// opponent-applied Toughness debuff scaled dimensionless by the curve's constant), Momentum
        /// (<see cref="BattleStats.MomentumBonusDealt"/>, #1428, on the attacker's own applied ramp), and Cull
        /// (<see cref="BattleStats.CullBonusDealt"/>, #1430, on the sampled execute investment). No tally runs a
        /// counterfactual curve evaluation, none re-reads the target's debuffed mitigation, and an absorbed
        /// portion trains nothing — see the loop comment for the rationale. Cull's enabler
        /// (<see cref="EAttribute.ExecuteBonus"/> scaled by the target's missing-health fraction, sampled once per
        /// fire) also multiplies the <b>real</b> damage dealt, identically to <see cref="EAttribute.CriticalDamage"/>
        /// and before mitigation, so it is the one overlay that is parity-critical beyond its tally. Each
        /// portion's absorption heal is capped at
        /// the <see cref="EAttribute.MaxHealth"/> room <b>at that point in the fixed portion order</b>, so the order is
        /// a parity contract (only reachable through authored absorption — resistance &gt; 1 — on one portion of a
        /// multi-typed hit). <b>Reflection runs once</b> on <c>totalNet</c> (spike #1330): it is linear and
        /// untyped, so once equals per-portion, and once gives a single clean reflection event. The Toughness curve
        /// divides by a <b>tuned constant</b> (#1487/#1489), so it is attacker-level-independent. Enemies never
        /// crit/dodge/parry: the gating reads the rolls only on the player's side.
        /// </summary>
        /// <returns>
        /// The summed net damage applied across all portions (after amplification, crit, resistance, and the
        /// Toughness curve) — the same value booked into the battle stats — so the caller can record a
        /// reconciling per-skill total instead of the raw pre-mitigation hit. Reflected damage dealt back to the
        /// attacker is booked separately and is not part of this return.
        /// </returns>
        public double DamageTarget(double rawDamage, IReadOnlyList<SkillDamagePortion> portions, double baseCriticalChance)
        {
            // Fold the total weight in fixed portion order (a parity contract — float addition is not associative),
            // so each portion takes its raw × weight ÷ Σweights share of the one raw hit. Computing it draws no RNG.
            // Relies on the Skill.DamagePortions invariant (≥1 positive-weight portion, enforced by authoring
            // validation + backfill), so totalWeight is never 0 — no zero-division/empty-hit guard is needed here.
            var totalWeight = 0.0;
            for (var i = 0; i < portions.Count; i++)
            {
                totalWeight += portions[i].Weight;
            }

            var totalNet = 0.0;
            if (_isPlayerActive)
            {
                // ONE crit draw per player fire, before any damage and independent of portion count. The rolled
                // chance is the firing skill's own base (0 for an unauthored skill, the per-skill opt-in enabler)
                // scaled by the active battler's CriticalChanceMultiplier (base 1, so an uncommitted skill still
                // crits at its own authored rate and further investment scales it). A single crit multiplies
                // every portion (× 1.0 when it misses is exact, so a non-crit is unchanged).
                totalNet = ResolvePlayerHit(rawDamage, portions, totalWeight, baseCriticalChance, _rng.Next());
            }
            else
            {
                // Three draws per enemy fire, in fixed order — parry (#1457), dodge (#1523; Block's former
                // second draw was retired, spike #1330), and the parry counter's crit — all taken unconditionally
                // so the seeded stream advances as a pure function of the fire sequence, never of a roll outcome
                // or of the defender's build (a 0-chance draw still consumes). A parry takes precedence over a dodge.
                var isParry = _rng.Next() < _targetBattler.GetAttributeValue(ParryChance)
                    * _targetBattler.GetAttributeValue(ParryChanceMultiplier);
                var isDodge = _rng.Next() < _targetBattler.GetAttributeValue(DodgeChance)
                    * _targetBattler.GetAttributeValue(DodgeChanceMultiplier);
                var counterCritDraw = _rng.Next();

                if (isParry)
                {
                    // A parry negates the whole hit exactly like a dodge — the avoided damage is each portion's
                    // net (resistance then the Toughness curve) computed without mutating health, and no exposure
                    // is recorded — and then ripostes with the defender's counter skill.
                    Stats.AttacksParried++;
                    for (var i = 0; i < portions.Count; i++)
                    {
                        var dealt = AmplifiedPortion(rawDamage, totalWeight, portions[i]);
                        Stats.DamageParried += _targetBattler.ComputeNetDamage(dealt, portions[i].Type);
                    }

                    FireParryCounter(counterCritDraw);
                }
                else if (isDodge)
                {
                    // A dodge zeroes the whole hit; the avoided damage is the sum of each portion's net (resistance
                    // then the Toughness curve), computed without mutating health. A dodge evaded the hit
                    // entirely, so no exposure is recorded (it trains evasion instead).
                    Stats.AttacksDodged++;
                    for (var i = 0; i < portions.Count; i++)
                    {
                        var dealt = AmplifiedPortion(rawDamage, totalWeight, portions[i]);
                        Stats.DamageDodged += _targetBattler.ComputeNetDamage(dealt, portions[i].Type);
                    }
                }
                else
                {
                    for (var i = 0; i < portions.Count; i++)
                    {
                        var type = portions[i].Type;
                        var dealt = AmplifiedPortion(rawDamage, totalWeight, portions[i]);
                        // The player was exposed to this portion's full pre-mitigation typed damage — recorded for
                        // the incoming book before resistance/mitigation — and separately the resistance-only
                        // (Toughness-excluded) slice of it the player's own type-resistance blocked, feeding the
                        // resist-training split (#1454).
                        Stats.AddTypedDamageExposure(type, dealt);
                        Stats.AddTypedDamageResistanceMitigated(type, _targetBattler.TypeResistanceMitigated(dealt, type));
                        totalNet += _targetBattler.TakeDamage(dealt, type);
                    }
                }

                Stats.PlayerDamageTaken += totalNet;
            }

            ReflectDamage(totalNet);
            return totalNet;
        }

        /// <summary>
        /// The player-fire half of <see cref="DamageTarget"/>: resolves one player hit's crit (from the
        /// already-taken <paramref name="critDraw"/>), execute multiplier, per-portion amplification /
        /// mitigation / typed booking / overlay share claims, and the whole-hit crit and highest-attack stats.
        /// Extracted so the parry counter (#1457) fires through the <b>identical</b> pipeline as a normal
        /// player fire — same crit template, same execute and overlay tallies, same typed offense booking —
        /// with its crit draw supplied by the caller (the enemy fire's third unconditional draw) rather than
        /// drawn here. Requires the player to be the active battler.
        /// </summary>
        private double ResolvePlayerHit(
            double rawDamage, IReadOnlyList<SkillDamagePortion> portions, double totalWeight,
            double baseCriticalChance, double critDraw)
        {
            var isCrit = critDraw < baseCriticalChance * _activeBattler.GetAttributeValue(CriticalChanceMultiplier);
            var critMultiplier = isCrit ? _activeBattler.GetAttributeValue(CriticalDamage) : 1.0;

            // The Cull execute overlay (#1430): the target's missing-health fraction AT THE START of this
            // fire, sampled once (like the crit draw) so an earlier portion's damage within the same
            // multi-typed hit cannot change a later portion's bonus. executeInvestment is this fire's
            // multiplier above 1 — 0 when ExecuteBonus is unauthored or the target is at full health —
            // applied to the raw damage identically to critMultiplier, before mitigation.
            var targetMaxHealth = _targetBattler.GetAttributeValue(MaxHealth);
            var missingHpFraction = Math.Clamp(
                (targetMaxHealth - _targetBattler.CurrentHealth) / targetMaxHealth, 0.0, 1.0);
            var executeInvestment = _activeBattler.GetAttributeValue(ExecuteBonus) * missingHpFraction;
            var executeMultiplier = 1.0 + executeInvestment;

            // The Frequency cadence pseudo-overlay (#1426/#1527): the player's effective charge rate above the
            // base-1 normal cadence — CooldownRecovery + CooldownBonus × CooldownBonusMultiplier − 1 — sampled once
            // per fire (like the execute investment), since faster cycling IS more hits and so every landed hit
            // rides it. Unlike the enabler multiplier it does NOT touch the real damage — it only scales the tally
            // below. An uncommitted build sits at exactly 1.0, so the investment is 0 and it books nothing.
            var cadenceInvestment = _activeBattler.GetCooldownMultiplier() - 1.0;

            var totalNet = 0.0;
            var totalBooked = 0.0;
            for (var i = 0; i < portions.Count; i++)
            {
                var type = portions[i].Type;
                var rawPortion = PortionRawDamage(rawDamage, totalWeight, portions[i]);
                var dealt = _activeBattler.AmplifyDamage(rawPortion, type);
                var healthBefore = _targetBattler.CurrentHealth;
                var net = _targetBattler.TakeDamage(dealt * critMultiplier * executeMultiplier, type);
                // The typed offense book is capped at the health the portion actually removed (#1482): a
                // killing swing's overkill tail — and every later portion of it — books nothing, while the
                // whole-hit stats below keep the full net (feedback like HighestPlayerAttack includes overkill).
                var booked = Battler.HealthRemoved(net, healthBefore);
                Stats.AddTypedDamageDealt(type, booked);
                totalNet += net;

                // Every overlay tally books a share claim on the damage this portion actually landed (#1481):
                // the booked (health-capped) net × φ(its own investment) — one uniform shape with no
                // counterfactual curve evaluations, structurally immune to cross-overlay inflation (the basis
                // never re-reads the target's debuffed mitigation) and bounded per battle by the enemy's
                // health pool. An absorbed portion (negative booked) trains nothing. All backend-only side
                // channels — no health mutation, no parity surface.
                var overlayBasis = booked > 0 ? booked : 0;
                totalBooked += overlayBasis;
                // Hex (#1427): the target-side applied-vulnerability share — zero unless the player's own
                // debuff lowered the target's resistance.
                Stats.HexBonusDealt += _targetBattler.HexBonusForHit(overlayBasis, type);
                // Sunder (#1429): the target-side Toughness-debuff share, the investment made dimensionless
                // by the mitigation curve's own constant magnitude.
                Stats.SunderBonusDealt += _targetBattler.SunderBonusForHit(overlayBasis);
                // Momentum (#1428): the attacker's own applied-ramp share for this portion's type.
                var rampContribution = _activeBattler.AppliedMomentum(type);
                if (rampContribution > 0)
                {
                    Stats.MomentumBonusDealt += overlayBasis * Battler.NormalizeInvestment(rampContribution);
                }
                // Cull (#1430): the execute-investment share. The executeMultiplier still scales the real
                // damage above — that part stays parity-critical and unchanged; only the tally is reshaped.
                if (executeInvestment > 0)
                {
                    Stats.CullBonusDealt += overlayBasis * Battler.NormalizeInvestment(executeInvestment);
                }
            }

            // Frequency (#1426/#1527): the cadence pseudo-overlay share claim — the whole fire's booked
            // (health-capped) damage × φ(effective cadence − 1). Books on every landed hit whose cadence is above
            // the base-1 rate (φ is constant per fire, so booking totalBooked once equals booking each portion),
            // so a faster-cycling build's per-battle claim scales with its cadence investment while still summing
            // to at most the enemy's health pool — the same enemy-authored share every overlay tally holds.
            if (cadenceInvestment > 0)
            {
                Stats.CadenceBonusDealt += totalBooked * Battler.NormalizeInvestment(cadenceInvestment);
            }

            Stats.PlayerDamageDealt += totalNet;
            if (isCrit)
            {
                Stats.CriticalHits++;
                // The actual crit damage dealt — the player-facing CriticalDamageDealt statistic.
                Stats.CriticalDamageDealt += totalNet;
                // The Precision training signal (#1448, reshaped by #1481): the same share claim as the other
                // overlays, on the whole crit hit's booked damage — φ on the crit-damage investment, so a
                // heavier investment trains proportionally more while a single crit claims at most its own
                // booked hit. Kept separate from the player-facing statistic above. Guarded like every sibling
                // overlay (#1927): a CriticalDamage debuff at or below 1.0 is a non-positive investment and
                // trains nothing, rather than booking a negative (or, at exactly 1.0, a −∞) claim.
                if (critMultiplier > 1.0)
                {
                    Stats.CriticalBonusDealt += totalBooked * Battler.NormalizeInvestment(critMultiplier - 1.0);
                }
            }

            if (totalNet > Stats.HighestPlayerAttack)
            {
                Stats.HighestPlayerAttack = totalNet;
            }

            return totalNet;
        }

        /// <summary>
        /// Fires the player's riposte after a parry (#1457): the defender (player) strikes back with its
        /// <see cref="Battler.CounterSkill"/> — the equipped weapon's signature, resolved at battler assembly —
        /// as a <b>first-class player hit</b> through <see cref="ResolvePlayerHit"/>, so amplification, the
        /// signature's own authored <see cref="Skills.Skill.CriticalChance"/> (scaled by
        /// <see cref="EAttribute.CriticalChanceMultiplier"/>), the execute multiplier, the overlay share
        /// claims, typed offense booking, and the enemy's reflection of the counter all apply exactly as they
        /// would to a deliberate fire. Damage only — a <b>phantom</b> fire: no effects are applied, no cooldown
        /// is touched, and no per-skill use is recorded. Its crit decision consumes the already-taken
        /// <paramref name="counterCritDraw"/> (the enemy fire's third unconditional draw), so a defender with
        /// no resolvable counter skill (which parries without a riposte) leaves the stream identical. The
        /// counter's net is additionally booked as <see cref="BattleStats.PlayerCounterDamageDealt"/> — the
        /// Riposte training signal, a direct tally like Retribution's.
        /// </summary>
        private void FireParryCounter(double counterCritDraw)
        {
            var counterSkill = _targetBattler.CounterSkill;
            if (counterSkill is null)
            {
                return;
            }

            SwapActiveAndTargetBattlers();
            var raw = BattleSkill.CalculateRawDamage(counterSkill, _activeBattler);
            var counterPortions = counterSkill.DamagePortions;
            var totalWeight = 0.0;
            for (var i = 0; i < counterPortions.Count; i++)
            {
                totalWeight += counterPortions[i].Weight;
            }

            var counterNet = ResolvePlayerHit(
                raw, counterPortions, totalWeight, counterSkill.CriticalChance, counterCritDraw);
            Stats.PlayerCounterDamageDealt += counterNet;
            // The enemy's deterministic reflection applies to the counter like any direct hit it takes (the
            // counter is a genuine attack, subject to the target's defenses — unlike reflection itself, which
            // never chains: reflected damage is not routed through this pipeline).
            ReflectDamage(counterNet);
            SwapActiveAndTargetBattlers();
        }

        /// <summary>
        /// One portion's pre-amplification share of the single raw hit: <c>rawDamage × weight ÷ totalWeight</c>,
        /// the fixed-order fold of the weights done by the caller. A method call rather than an inline expression
        /// so the per-portion loops cannot diverge in their split arithmetic.
        /// </summary>
        private double PortionRawDamage(double rawDamage, double totalWeight, SkillDamagePortion portion)
        {
            return rawDamage * portion.Weight / totalWeight;
        }

        /// <summary>
        /// One portion's attacker-amplified pre-mitigation damage — <see cref="PortionRawDamage"/> run through the
        /// active (attacking) battler's typed amplification.
        /// </summary>
        private double AmplifiedPortion(double rawDamage, double totalWeight, SkillDamagePortion portion)
        {
            return _activeBattler.AmplifyDamage(PortionRawDamage(rawDamage, totalWeight, portion), portion.Type);
        }

        /// <summary>
        /// Applies deterministic damage reflection (spike #1330): the defender (<see cref="_targetBattler"/>)
        /// returns its <see cref="EAttribute.DamageReflection"/> share of a direct hit's net damage to the
        /// attacker (<see cref="_activeBattler"/>), bypassing the attacker's own mitigation. Only a positive net
        /// reflects — a dodged or absorbed hit returns nothing — and DoT is never routed here, so DoT is never
        /// reflected. The reflected amount is booked as the player's damage taken (when the enemy reflects onto
        /// the player) or dealt (when the player reflects onto the enemy), keeping the running totals reconciled
        /// with the health change. The player-reflected case is additionally captured as the dedicated
        /// <see cref="BattleStats.PlayerReflectedDamageDealt"/> Retribution signal (#1363).
        /// </summary>
        private void ReflectDamage(double netDamage)
        {
            if (netDamage <= 0)
            {
                return;
            }

            var reflection = _targetBattler.GetAttributeValue(DamageReflection);
            if (reflection <= 0)
            {
                return;
            }

            var reflected = netDamage * reflection;
            _activeBattler.TakeReflectedDamage(reflected);
            if (_isPlayerActive)
            {
                Stats.PlayerDamageTaken += reflected;
            }
            else
            {
                // The enemy is attacking and the player (defender) reflected: this is the player's reflected
                // damage dealt — both part of the untyped damage total and the dedicated Retribution signal.
                Stats.PlayerDamageDealt += reflected;
                Stats.PlayerReflectedDamageDealt += reflected;
            }
        }

        public void RecordSkillUse(int skillId, double damage)
        {
            if (_isPlayerActive)
            {
                Stats.PlayerSkillsUsed++;
                if (!Stats.SkillStats.TryGetValue(skillId, out var value))
                {
                    value = new SkillStats();
                    Stats.SkillStats[skillId] = value;
                }

                value.Uses++;
                value.TotalDamage += damage;
                if (damage > value.HighestSingleAttack)
                {
                    value.HighestSingleAttack = damage;
                }

            }
        }
    }
}
