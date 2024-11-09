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

        public double CalculateDamage(BattleAttributes atts)
        {
            double damage = (double)Data.BaseDamage;
            Data.SkillDamageMultipliers.ForEach((dmgType) =>
            {
                damage += atts[(EAttribute)dmgType.AttributeId] * (double)dmgType.Multiplier;
            });
            return damage;
        }
    }
}
