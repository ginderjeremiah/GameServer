using DataAccess.Caches;
using GameServer.Auth;

namespace GameServer.BattleSimulation
{
    public class BattlePlayer : Battler
    {
        public override BattleBaseStats Stats { get; set; }
        public override DerivedStats DerivedStats { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattlePlayer(PlayerData playerData, ISkillCache skillCache)
        {
            Stats = playerData.Stats;
            DerivedStats = new DerivedStats(Stats);
            CurrentHealth = DerivedStats.MaxHealth;
            Skills = playerData.GetSkills(skillCache).Select(skill => new BattleSkill(skill)).ToList();
            Level = playerData.Level;
        }
    }

    /*public class BattlePlayer
    {
        private readonly BaseStats baseStats;
        private readonly DerivedStats derivedStats;
        private double currentHealth;
        private readonly List<BattleSkill> skills;
        public bool IsDead { get { return currentHealth <= 0; } }
        public BattlePlayer(PlayerData playerData)
        {
            baseStats = playerData.Stats;
            derivedStats = new DerivedStats(baseStats);
            currentHealth = derivedStats.MaxHealth;
            skills = playerData.Skills.Select(skill => new BattleSkill(skill)).ToList();
        }

        public List<BattleSkill> AdvancedCooldowns(int timeDelta)
        {
            var firedSkills = new List<BattleSkill>();
            var cdMultiplier = (1 + derivedStats.CooldownRecovery) / 100;
            foreach (var skill in skills)
            {
                skill.ChargeTime += timeDelta * cdMultiplier;
                if (skill.ChargeTime > skill.Data.CooldownMS)
                {
                    firedSkills.Add(skill);
                    skill.ChargeTime = 0;
                }
            }
            return firedSkills;
        }

        public bool TakeDamage(double rawDamage)
        {
            var damage = rawDamage - derivedStats.Defense;
            damage = damage > 0 ? damage : 0;
            currentHealth -= damage;
            return IsDead;
        }
    }*/
}
