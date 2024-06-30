namespace GameCore.BattleSimulation
{
    public class DerivedAttributes
    {
        public double MaxHealth { get; set; }
        public double Defense { get; set; }
        public double CooldownRecovery { get; set; }
        public double DropMod { get; set; }
        public DerivedAttributes(BattleAttributes attributes)
        {
            MaxHealth = 50 + 20 * attributes[EAttribute.Endurance] + 5 * attributes[EAttribute.Strength];
            Defense = 2 + attributes[EAttribute.Endurance] + 0.5 * attributes[EAttribute.Agility];
            CooldownRecovery = 0.4 * attributes[EAttribute.Agility] + 0.1 * attributes[EAttribute.Dexterity];
            DropMod = Math.Log10(attributes[EAttribute.Luck]);
        }
    }
}
