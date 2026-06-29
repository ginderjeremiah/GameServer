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

        // The player's typed-exposure recorder, cached once (a method group converts to a fresh delegate on
        // each use) so the per-tick DoT phase can hand it to ApplyDamageOverTime without allocating on the
        // replay hot path.
        private readonly Action<EDamageType, double> _recordPlayerExposure;

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
        }

        public void SwapActiveAndTargetBattlers()
        {
            (_targetBattler, _activeBattler) = (_activeBattler, _targetBattler);
            _isPlayerActive = !_isPlayerActive;
        }

        public double GetActiveBattlerAttribute(EAttribute attribute)
        {
            return _activeBattler.GetAttributeValue(attribute);
        }

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

            var battler = effect.Target is ESkillEffectTarget.Self ? _activeBattler : _targetBattler;
            battler.ApplyEffect(effect, amount);
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
            // The enemy's DoT (the player's DoT damage dealt) is the offense DoT book, tracked separately
            // (#1338), so no exposure recorder is passed here; the player's incoming DoT records its
            // pre-mitigation exposure into the incoming book via the cached recorder.
            Stats.PlayerDamageDealt += _enemyBattler.ApplyDamageOverTime(TimeDelta);
            _enemyBattler.ApplyHealOverTime(TimeDelta);
            if (_enemyBattler.IsDead)
            {
                return;
            }

            Stats.PlayerDamageTaken += _playerBattler.ApplyDamageOverTime(TimeDelta, _recordPlayerExposure);
            Stats.PlayerDamageHealed += _playerBattler.ApplyHealOverTime(TimeDelta);
        }

        /// <summary>
        /// Deals one skill hit's <paramref name="rawDamage"/> to the current target, drawing the per-fire
        /// seeded RNG and applying the player-only crit/dodge/block rolls (gated on the acting battler). The
        /// draw order is a pure function of the skill-fire sequence — never of a roll outcome — so the per-tick
        /// draw count stays <c>playerFires×1 + enemyFires×2</c>:
        /// <list type="bullet">
        /// <item>When the <b>player</b> attacks, a single crit draw is taken (always, before damage). On a crit
        /// the raw damage is multiplied by <see cref="EAttribute.CriticalDamage"/> (read directly) <b>before</b>
        /// mitigation, so high crit damage punches through <see cref="EAttribute.Toughness"/>.</item>
        /// <item>When the <b>enemy</b> attacks the player, two draws are taken unconditionally — dodge then
        /// block, <b>both</b> always drawn (even when the hit is dodged). A dodge zeroes the hit; a non-dodged
        /// block applies <see cref="EAttribute.BlockReduction"/> as a flat reduction after the Toughness
        /// curve.</item>
        /// </list>
        /// The Toughness curve scales by the <b>active (attacking) battler's level</b>, so the mitigation band
        /// stays stable as content scales. Enemies never crit/dodge/block: the gating reads the rolls only on the
        /// player's side, leaving a clean seam for later enemy parity.
        /// </summary>
        /// <returns>
        /// The actual damage applied after amplification, crit, resistance, the Toughness curve, and block — the
        /// same value booked into the battle stats — so the caller can record a reconciling per-skill total
        /// instead of the raw pre-mitigation hit.
        /// </returns>
        public double DamageTarget(double rawDamage, EDamageType damageType)
        {
            // Attacker-side amplification first (before the crit draw, which it never feeds), so the typed hit
            // entering the mitigation pipeline is the amplified value. Computing it here doesn't advance the RNG.
            var dealt = _activeBattler.AmplifyDamage(rawDamage, damageType);

            double actualDamage;
            if (_isPlayerActive)
            {
                var isCrit = _rng.Next() < _activeBattler.GetAttributeValue(CriticalChance);
                var damage = isCrit ? dealt * _activeBattler.GetAttributeValue(CriticalDamage) : dealt;
                actualDamage = _targetBattler.TakeDamage(damage, damageType, _activeBattler.Level);

                Stats.PlayerDamageDealt += actualDamage;
                Stats.AddTypedDamageDealt(damageType, actualDamage);
                if (isCrit)
                {
                    Stats.CriticalHits++;
                    Stats.CriticalDamageDealt += actualDamage;
                }

                if (actualDamage > Stats.HighestPlayerAttack)
                {
                    Stats.HighestPlayerAttack = actualDamage;
                }
            }
            else
            {
                // Both draws are always taken (dodge then block), even on a dodge, so the stream never
                // branches on a roll result.
                var isDodge = _rng.Next() < _targetBattler.GetAttributeValue(DodgeChance);
                var isBlock = _rng.Next() < _targetBattler.GetAttributeValue(BlockChance);

                // The net damage the hit would deal if it landed normally (resistance then the Toughness curve, no
                // block) — the basis for how much a dodge avoided or a block prevented, via the same pipeline
                // Battler.TakeDamage applies. The curve scales by the attacking (active) battler's level.
                var afterMitigation = _targetBattler.ComputeNetDamage(dealt, damageType, _activeBattler.Level);

                if (isDodge)
                {
                    actualDamage = 0;
                    Stats.AttacksDodged++;
                    Stats.DamageDodged += afterMitigation;
                }
                else
                {
                    // The hit landed (blocked or not), so the player was exposed to its full pre-mitigation
                    // typed damage — recorded for the incoming book before resistance/mitigation. A dodge above
                    // evaded the hit entirely, so it is excluded (its avoided damage trains evasion instead).
                    Stats.AddTypedDamageExposure(damageType, dealt);
                    if (isBlock)
                    {
                        actualDamage = _targetBattler.TakeDamage(dealt, damageType, _activeBattler.Level, _targetBattler.GetAttributeValue(BlockReduction));
                        Stats.AttacksBlocked++;
                        Stats.DamageBlocked += afterMitigation - actualDamage;
                    }
                    else
                    {
                        actualDamage = _targetBattler.TakeDamage(dealt, damageType, _activeBattler.Level);
                    }
                }

                Stats.PlayerDamageTaken += actualDamage;
            }

            return actualDamage;
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
