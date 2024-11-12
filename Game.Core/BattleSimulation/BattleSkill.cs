using Game.Core.Entities;

namespace Game.Core.BattleSimulation
{
    public class BattleSkill
    {
        public double ChargeTime { get; set; } = 0;
        public Skill Data;

        public BattleSkill(Skill skillData)
        {
            Data = skillData;
        }

        public double CalculateDamage(BattleAttributes attributes)
        {
            double damage = (double)Data.BaseDamage;
            Data.SkillDamageMultipliers.ForEach((dmgType) =>
            {
                damage += attributes[(EAttribute)dmgType.AttributeId] * (double)dmgType.Multiplier;
            });
            return damage;
        }
    }
}
