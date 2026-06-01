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

        public void RecordSkillUse()
        {
            if (_isPlayerActive)
            {
                Stats.PlayerSkillsUsed++;
            }
        }
    }
}
