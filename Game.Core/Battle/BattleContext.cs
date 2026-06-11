using Game.Core.Skills;

namespace Game.Core.Battle
{
    public class BattleContext
    {
        private Battler _activeBattler;
        private Battler _targetBattler;
        private bool _isPlayerActive;

        public int TimeDelta { get; set; }
        public BattleStats Stats { get; } = new();

        public BattleContext(Battler playerBattler, Battler enemyBattler, int timeDelta)
        {
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
        /// <see cref="ESkillEffectTarget.Opponent"/> to the target battler.
        /// </summary>
        public void ApplySkillEffect(SkillEffect effect)
        {
            var battler = effect.Target is ESkillEffectTarget.Self ? _activeBattler : _targetBattler;
            battler.ApplyEffect(effect);
        }

        public void DamageTarget(double damage)
        {
            var actualDamage = _targetBattler.TakeDamage(damage);

            if (_isPlayerActive)
            {
                Stats.PlayerDamageDealt += actualDamage;
                if (actualDamage > Stats.HighestPlayerAttack)
                {
                    Stats.HighestPlayerAttack = actualDamage;
                }
            }
            else
            {
                Stats.PlayerDamageTaken += actualDamage;
            }
        }

        public void RecordSkillUse(int skillId, double damage)
        {
            if (_isPlayerActive)
            {
                Stats.PlayerSkillsUsed++;
                if (!Stats.SkillStats.TryGetValue(skillId, out var value))
                {
                    value = new SkillStats { SkillId = skillId };
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
