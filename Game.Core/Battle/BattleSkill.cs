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
            // Accumulate the multiplier bonus separately and add BaseDamage last, exactly mirroring the
            // previous `BaseDamage + DamageMultipliers.Sum(...)`. Floating-point addition is not
            // associative, so preserving this order keeps the result bit-for-bit identical — required
            // for frontend/backend battle parity. The manual loop avoids the per-fire allocations the
            // LINQ Sum incurred on this hot path (a boxed list enumerator plus a closure capturing
            // `context`, allocated on every skill fire) — see #286.
            var multiplierBonus = 0.0;
            foreach (var multiplier in Skill.DamageMultipliers)
            {
                multiplierBonus += context.GetActiveBattlerAttribute(multiplier.Attribute) * multiplier.Amount;
            }

            return Skill.BaseDamage + multiplierBonus;
        }
    }
}
