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
        /// <c>ScalingAmount</c> of <c>0</c> leaves the authored amount unchanged.
        /// </summary>
        public void ApplySkillEffect(SkillEffect effect)
        {
            var amount = effect.Amount + _activeBattler.GetAttributeValue(effect.ScalingAttributeId) * effect.ScalingAmount;
            var battler = effect.Target is ESkillEffectTarget.Self ? _activeBattler : _targetBattler;
            battler.ApplyEffect(effect, amount);
        }

        /// <summary>
        /// Resolves the end-of-tick damage/heal-over-time phase for both battlers, recording its statistics.
        /// Called only when both battlers are still alive after the skill exchange. For each battler its
        /// <see cref="EAttribute.DamageTakenPerSecond"/> (bypassing Defense) is applied, then its
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
            Stats.PlayerDamageDealt += _enemyBattler.ApplyDamageOverTime(TimeDelta);
            _enemyBattler.ApplyHealOverTime(TimeDelta);
            if (_enemyBattler.IsDead)
            {
                return;
            }

            Stats.PlayerDamageTaken += _playerBattler.ApplyDamageOverTime(TimeDelta);
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
        /// Defense is subtracted, so high crit damage punches through Defense.</item>
        /// <item>When the <b>enemy</b> attacks the player, two draws are taken unconditionally — dodge then
        /// block, <b>both</b> always drawn (even when the hit is dodged). A dodge zeroes the hit; a non-dodged
        /// block applies <see cref="EAttribute.BlockReduction"/> as a second flat reduction alongside
        /// Defense.</item>
        /// </list>
        /// Enemies never crit/dodge/block: the gating reads the rolls only on the player's side, leaving a clean
        /// seam for later enemy parity.
        /// </summary>
        /// <returns>
        /// The actual damage applied after crit, Defense, and block — the same value booked into the battle
        /// stats — so the caller can record a reconciling per-skill total instead of the raw pre-mitigation hit.
        /// </returns>
        public double DamageTarget(double rawDamage)
        {
            double actualDamage;
            if (_isPlayerActive)
            {
                var isCrit = _rng.Next() < _activeBattler.GetAttributeValue(CriticalChance);
                var damage = isCrit ? rawDamage * _activeBattler.GetAttributeValue(CriticalDamage) : rawDamage;
                actualDamage = _targetBattler.TakeDamage(damage);

                Stats.PlayerDamageDealt += actualDamage;
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

                // The post-Defense damage the hit would deal if it landed normally — the basis for how much a
                // dodge avoided or a block prevented (the same Defense clamp Battler.TakeDamage applies).
                var afterDefense = rawDamage - _targetBattler.GetAttributeValue(Defense);
                afterDefense = afterDefense > 0 ? afterDefense : 0;

                if (isDodge)
                {
                    actualDamage = 0;
                    Stats.AttacksDodged++;
                    Stats.DamageDodged += afterDefense;
                }
                else if (isBlock)
                {
                    actualDamage = _targetBattler.TakeDamage(rawDamage, _targetBattler.GetAttributeValue(BlockReduction));
                    Stats.AttacksBlocked++;
                    Stats.DamageBlocked += afterDefense - actualDamage;
                }
                else
                {
                    actualDamage = _targetBattler.TakeDamage(rawDamage);
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
