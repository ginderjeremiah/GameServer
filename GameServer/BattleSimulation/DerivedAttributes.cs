using DataAccess;

namespace GameServer.BattleSimulation
{
    public class DerivedAttributes
    {
        public double MaxHealth { get; set; }
        public double Defense { get; set; }
        public double CooldownRecovery { get; set; }
        public double DropMod { get; set; }
        public DerivedAttributes(BattleAttributes attributes)
        {
            MaxHealth = 50 + 20 * attributes[AttributeType.Endurance] + 5 * attributes[AttributeType.Strength];
            Defense = 2 + attributes[AttributeType.Endurance] + 0.5 * attributes[AttributeType.Agility];
            CooldownRecovery = 0.4 * attributes[AttributeType.Agility] + 0.1 * attributes[AttributeType.Dexterity];
            DropMod = Math.Log10(attributes[AttributeType.Luck]);
        }
    }
}
