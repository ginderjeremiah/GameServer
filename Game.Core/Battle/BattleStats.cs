namespace Game.Core.Battle
{
    public class BattleStats
    {
        public double PlayerDamageDealt { get; set; }
        public double PlayerDamageTaken { get; set; }
        public double HighestPlayerAttack { get; set; }
        public int PlayerSkillsUsed { get; set; }
        public Dictionary<int, SkillStats> SkillStats { get; set; } = [];
    }

    public class SkillStats
    {
        public int SkillId { get; set; }
        public int Uses { get; set; }
        public double TotalDamage { get; set; }
        public double HighestSingleAttack { get; set; }
    }
}
