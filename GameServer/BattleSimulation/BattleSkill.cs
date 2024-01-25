using DataAccess.Models.Skills;

namespace GameServer.BattleSimulation
{
    public class BattleSkill
    {
        public double ChargeTime { get; set; } = 0;
        public Skill Data;
        public BattleSkill(Skill skillData)
        {
            Data = skillData;
        }

        public double CalculateDamage(BattleBaseStats stats)
        {
            double damage = Data.BaseDamage;
            Data.DamageMultipliers.ForEach((dmgType) =>
            {
                damage += stats[dmgType.AttributeName] * (double)dmgType.Multiplier;
            });
            return damage;
        }
    }
}
