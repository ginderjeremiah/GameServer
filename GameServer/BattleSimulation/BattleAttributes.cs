using GameServer.Models.Attributes;
using static GameServer.AttributeType;

namespace GameServer.BattleSimulation
{
    public class BattleAttributes
    {
        private static readonly int _attributesMaxId = (int)Enum.GetValues(typeof(AttributeType)).GetValue(Enum.GetValues(typeof(AttributeType)).Length - 1);
        private readonly List<decimal> _attributes;

        //public double this[int index]
        //{
        //    get => (double)_attributes[index];
        //    set => _attributes[index] = (decimal)value;
        //}

        public double this[AttributeType index]
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
            return Enumerable.Repeat(0m, _attributesMaxId).ToList();
        }

        private void CalculateDerivedValues()
        {
            this[MaxHealth] += 50.0 + (20.0 * this[Endurance]) + (5 * this[Strength]);
            this[Defense] += 2.0 + this[Endurance] + (0.5 * this[Agility]);
            this[CooldownRecovery] += (0.4 * this[Agility]) + (0.1 * this[Dexterity]);
            this[DropBonus] += Math.Log10(this[Luck]);
            //this[CriticalChance] = 0.0;
            //this[CriticalDamage] = 0.0;
            //this[DodgeChance] = 0.0;
            //this[BlockChance] = 0.0;
            //this[BlockReduction] = 0.0;
        }
    }
}
