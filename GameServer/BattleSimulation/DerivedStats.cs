namespace GameServer.BattleSimulation
{
    public class DerivedStats
    {
        public int MaxHealth { get; set; }
        public double Defense { get; set; }
        public double CooldownRecovery { get; set; }
        public double DropMod { get; set; }
        public DerivedStats(BattleBaseStats stats)
        {
            MaxHealth = 50 + 20 * stats.Endurance + 5 * stats.Strength;
            Defense = 2 + stats.Endurance + 0.5 * stats.Agility;
            CooldownRecovery = 0.4 * stats.Agility + 0.1 * stats.Dexterity;
            DropMod = Math.Log10(stats.Luck);
        }
    }
}
