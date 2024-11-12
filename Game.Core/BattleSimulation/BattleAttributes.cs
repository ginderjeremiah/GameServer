using static Game.Core.EAttribute;

namespace Game.Core.BattleSimulation
{
    /// <summary>
    /// Represents an aggregated collection of <see cref="BattlerAttribute"/>s.
    /// </summary>
    public class BattleAttributes
    {
        private static readonly int _attributesMaxId = GetMaxAttribute();
        private readonly List<double> _attributes = GetEmptyAttributeList();

        /// <summary>
        /// Gets the associated value of an attribute by its <see cref="EAttribute"/> representation.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>The value of the attribute.</returns>
        public double this[EAttribute index]
        {
            get => _attributes[(int)index];
            set => _attributes[(int)index] = value;
        }

        /// <summary>
        /// Creates a new aggregate from an <see cref="IEnumerable{T}"/> of <see cref="BattlerAttribute"/>.
        /// </summary>
        /// <param name="attributes"></param>
        public BattleAttributes(IEnumerable<BattlerAttribute> attributes)
        {
            foreach (var att in attributes)
            {
                this[att.AttributeId] += (double)att.Amount;
            }

            CalculateDerivedValues();
        }

        private static List<double> GetEmptyAttributeList()
        {
            return Enumerable.Repeat(0.0, _attributesMaxId + 1).ToList();
        }

        private static int GetMaxAttribute()
        {
            var attributeValues = Enum.GetValues<EAttribute>();
            return (int)attributeValues.Max();
        }

        private void CalculateDerivedValues()
        {
            this[MaxHealth] += 50.0 + (20.0 * this[Endurance]) + (5 * this[Strength]);
            this[Defense] += 2.0 + this[Endurance] + (0.5 * this[Agility]);
            this[CooldownRecovery] += (0.4 * this[Agility]) + (0.1 * this[Dexterity]);
            this[DropBonus] += this[Luck] > 0.0 ? Math.Log10(this[Luck]) : 0.0;
            //this[CriticalChance] = 0.0;
            //this[CriticalDamage] = 0.0;
            //this[DodgeChance] = 0.0;
            //this[BlockChance] = 0.0;
            //this[BlockReduction] = 0.0;
        }
    }
}
