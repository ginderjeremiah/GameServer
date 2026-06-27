namespace Game.Core.Battle
{
    public class BattleStats
    {
        public double PlayerDamageDealt { get; set; }
        public double PlayerDamageTaken { get; set; }
        public double PlayerDamageHealed { get; set; }
        public double HighestPlayerAttack { get; set; }
        public int PlayerSkillsUsed { get; set; }

        // Player-only crit/dodge/block outcomes, accumulated across the battle (enemies never crit/dodge/block).
        // The damage figures are post-Defense: crit damage is what the crit hits actually dealt, dodged damage is
        // the post-Defense hit avoided, and blocked damage is the reduction the block prevented.
        public int CriticalHits { get; set; }
        public double CriticalDamageDealt { get; set; }
        public int AttacksDodged { get; set; }
        public double DamageDodged { get; set; }
        public int AttacksBlocked { get; set; }
        public double DamageBlocked { get; set; }

        public Dictionary<int, SkillStats> SkillStats { get; set; } = [];
    }

    public class SkillStats
    {
        public int Uses { get; set; }
        public double TotalDamage { get; set; }
        public double HighestSingleAttack { get; set; }
    }
}
