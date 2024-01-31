using DataAccess.Models.Enemies;
using DataAccess.Models.Skills;
using DataAccess.Models.Stats;
using GameServer.Models.Common;

namespace GameServer.BattleSimulation
{
    public class BattleEnemy : Battler
    {
        private double _totalWeight;
        private int _totalStats;
        private int _statsDistributed;
        private double _weightDistributed;

        public override BattleBaseStats Stats { get; set; }
        public override DerivedStats DerivedStats { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }
        public BattleEnemy(Enemy enemy, EnemyInstance enemyInstance, List<Skill> allSkills)
        {
            Level = enemyInstance.EnemyLevel;
            Stats = GetStats(enemy.StatDistribution);
            enemyInstance.Stats = Stats;
            DerivedStats = new DerivedStats(Stats);
            CurrentHealth = DerivedStats.MaxHealth;
            Skills = enemy.SelectedSkills.Select(id => new BattleSkill(allSkills[id])).ToList();
        }
        private BattleBaseStats GetStats(BaseStatDistribution statDistribution)
        {
            _totalWeight = statDistribution.StrengthWeight + statDistribution.EnduranceWeight + statDistribution.IntellectWeight
                + statDistribution.AgilityWeight + statDistribution.DexterityWeight + statDistribution.LuckWeight;
            _totalStats = statDistribution.BaseStats + statDistribution.StatsPerLevel * Level;
            _statsDistributed = 0;
            _weightDistributed = 0;
            var baseStats = new BattleBaseStats
            {
                Strength = GetStatDist(statDistribution.StrengthWeight),
                Endurance = GetStatDist(statDistribution.EnduranceWeight),
                Intellect = GetStatDist(statDistribution.IntellectWeight),
                Agility = GetStatDist(statDistribution.AgilityWeight),
                Dexterity = GetStatDist(statDistribution.DexterityWeight),
                Luck = GetStatDist(statDistribution.LuckWeight)
            };
            return baseStats;
        }

        private int GetStatDist(int statWeight)
        {
            _weightDistributed += statWeight / _totalWeight;
            var prevStats = _statsDistributed;
            _statsDistributed = (int)Math.Round(_weightDistributed * _totalStats, 2);
            return _statsDistributed - prevStats;
        }
    }
}
