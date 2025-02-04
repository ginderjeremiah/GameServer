namespace Game.Core.Battle
{
    /// <summary>
    /// Used to store context information during the battle.
    /// </summary>
    public class BattleContext
    {
        /// <summary>
        /// The battler that is currently active.
        /// </summary>
        private Battler _activeBattler;

        /// <summary>
        /// The battler that is the target of any non-self targeting effects.
        /// </summary>
        private Battler _targetBattler;

        /// <summary>
        /// The time delta for the current logical tick.
        /// </summary>
        public int TimeDelta { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="BattleContext"/>.
        /// </summary>
        /// <param name="activeBattler"></param>
        /// <param name="targetBattler"></param>
        /// <param name="timeDelta"></param>
        public BattleContext(Battler activeBattler, Battler targetBattler, int timeDelta)
        {
            _activeBattler = activeBattler;
            _targetBattler = targetBattler;
            TimeDelta = timeDelta;
        }

        /// <summary>
        /// Swaps the active and target battlers.
        /// </summary>
        public void SwapActiveAndTargetBattlers()
        {
            (_targetBattler, _activeBattler) = (_activeBattler, _targetBattler);
        }

        /// <summary>
        /// Gets the value of the given <see cref="EAttribute"/> for the active battler.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public double GetActiveBattlerAttribute(EAttribute attribute)
        {
            return _activeBattler.GetAttributeValue(attribute);
        }

        /// <summary>
        /// Gets the active battler's cooldown multiplier.
        /// </summary>
        /// <returns></returns>
        public double GetActiveBattlerCooldownMultiplier()
        {
            return _activeBattler.GetCooldownMultiplier();
        }

        /// <summary>
        /// Damages the target battler.
        /// </summary>
        /// <param name="damage"></param>
        public void DamageTarget(double damage)
        {
            _targetBattler.TakeDamage(damage);
        }
    }
}
