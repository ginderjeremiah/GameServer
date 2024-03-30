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
            MaxHealth = 50 + 20 * attributes[Attributes.Endurance] + 5 * attributes[Attributes.Strength];
            Defense = 2 + attributes[Attributes.Endurance] + 0.5 * attributes[Attributes.Agility];
            CooldownRecovery = 0.4 * attributes[Attributes.Agility] + 0.1 * attributes[Attributes.Dexterity];
            DropMod = Math.Log10(attributes[Attributes.Luck]);
        }
    }
}
