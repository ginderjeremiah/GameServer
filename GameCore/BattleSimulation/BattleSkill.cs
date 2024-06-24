﻿using GameCore.Entities;

namespace GameCore.BattleSimulation
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
                damage += atts[(AttributeType)dmgType.AttributeId] * (double)dmgType.Multiplier;
            });
            return damage;
        }
    }
}
