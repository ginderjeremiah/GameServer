using Game.Core.Skills;

namespace Game.Core.Battle
{
    /// <summary>
    /// Wraps a <see cref="Skill"/> with runtime battle state (charge time).
    /// </summary>
    public class BattleSkill
    {
        public Skill Skill { get; }

        public double ChargeTime { get; set; }

        public BattleSkill(Skill skill)
        {
            Skill = skill;
            ChargeTime = 0.0;
        }

        public void Update(BattleContext context)
        {
            ChargeTime += context.TimeDelta * context.GetActiveBattlerCooldownMultiplier();
            if (ChargeTime >= Skill.CooldownMs)
            {
                ChargeTime = 0.0;
                var damage = CalculateDamage(context);
                context.RecordSkillUse(Skill.Id, damage);
                context.DamageTarget(damage);
            }
        }

        public double CalculateDamage(BattleContext context)
        {
            return Skill.BaseDamage + Skill.DamageMultipliers.Sum(mult => context.GetActiveBattlerAttribute(mult.Attribute) * mult.Amount);
        }
    }
}
