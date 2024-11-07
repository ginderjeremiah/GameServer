using Game.Core;
using static Game.Core.EAttribute;

namespace Game.Core.BattleSimulation
{
    public class BattleAttributes
    {
        private static readonly int _attributesMaxId = (int)Enum.GetValues(typeof(EAttribute)).GetValue(Enum.GetValues(typeof(EAttribute)).Length - 1);
        private readonly List<decimal> _attributes;

        public double this[EAttribute index]
        {
            get => (double)_attributes[(int)index];
            set => _attributes[(int)index] = (decimal)value;
        }

        public BattleAttributes(IEnumerable<BattlerAttribute> atts)
        {
            _attributes = GetEmptyAttributeList();
            foreach (var att in atts)
            {
                _attributes[(int)att.AttributeId] += att.Amount;
            }
            CalculateDerivedValues();
        }

        private static List<decimal> GetEmptyAttributeList()
        {
            return Enumerable.Repeat(0m, _attributesMaxId + 1).ToList();
        }

        private void CalculateDerivedValues()
        {
            this[MaxHealth] += 50.0 + 20.0 * this[Endurance] + 5 * this[Strength];
            this[Defense] += 2.0 + this[Endurance] + 0.5 * this[Agility];
            this[CooldownRecovery] += 0.4 * this[Agility] + 0.1 * this[Dexterity];
            this[DropBonus] += this[Luck] > 0.0 ? Math.Log10(this[Luck]) : 0.0;
            //this[CriticalChance] = 0.0;
            //this[CriticalDamage] = 0.0;
            //this[DodgeChance] = 0.0;
            //this[BlockChance] = 0.0;
            //this[BlockReduction] = 0.0;
        }
    }
}
