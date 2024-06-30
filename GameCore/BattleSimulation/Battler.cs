using static GameCore.EAttribute;

namespace GameCore.BattleSimulation
{
    public abstract class Battler
    {
        public abstract BattleAttributes Attributes { get; set; }
        public abstract double CurrentHealth { get; set; }
        public abstract List<BattleSkill> Skills { get; set; }
        public abstract int Level { get; set; }
        public bool IsDead { get { return CurrentHealth <= 0; } }

        public List<BattleSkill> AdvancedCooldowns(int timeDelta)
        {
            var firedSkills = new List<BattleSkill>();
            var cdMultiplier = 1 + (Attributes[CooldownRecovery] / 100);
            foreach (var skill in Skills)
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

        public void TakeDamage(double rawDamage)
        {
            var damage = rawDamage - Attributes[Defense];
            damage = damage > 0 ? damage : 0;
            CurrentHealth -= damage;
        }
    }
}