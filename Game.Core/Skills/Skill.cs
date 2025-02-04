using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;

namespace Game.Core.Skills
{
    /// <summary>
    /// Represents a skill that can be used in battle.
    /// </summary>
    public class Skill
    {
        /// <summary>
        /// The unique identifier of the skill.
        /// </summary>
        public required int Id { get; set; }

        /// <summary>
        /// The name of the skill.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The base amount of damage dealt by the skill before applying damage multipliers.
        /// </summary>
        public required double BaseDamage { get; set; }

        /// <summary>
        /// A description of the skill.
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// The cooldown of the skill in milliseconds.
        /// </summary>
        public required int CooldownMs { get; set; }

        ///// <summary>
        ///// The current amount of time in milliseconds the skill has been charged.
        ///// </summary>
        //public double ChargeTime { get; set; } = 0.0;

        ///// <summary>
        ///// Whether the skill is currently selected for battle.
        ///// </summary>
        //public bool IsSelected { get; set; } = false;

        /// <summary>
        /// The multipliers for each <see cref="EAttribute"/> that apply to the final damage of the skill.
        /// </summary>
        public required List<AttributeModifier> DamageMultipliers { get; set; }

        ///// <summary>
        ///// Creates a new instance of <see cref="Skill"/>.
        ///// </summary>
        ///// <returns></returns>
        //public Skill Clone()
        //{
        //    return new Skill
        //    {
        //        Name = Name,
        //        BaseDamage = BaseDamage,
        //        Description = Description,
        //        CooldownMs = CooldownMs,
        //        DamageMultipliers = DamageMultipliers
        //    };
        //}

        /// <summary>
        /// Advances the skill's charge time and applies damage if the skill cooldown is reached.
        /// </summary>
        /// <param name="context"></param>
        public void Update(BattleContext context)
        {
            ChargeTime += context.TimeDelta * context.GetActiveBattlerCooldownMultiplier();
            if (ChargeTime >= CooldownMs)
            {
                ChargeTime = 0.0;
                var damage = CalculateDamage(context);
                context.DamageTarget(damage);
            }
        }

        /// <summary>
        /// Calculates the amount of damage done by the skill.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public double CalculateDamage(BattleContext context)
        {
            return BaseDamage + DamageMultipliers.Sum(mult => context.GetActiveBattlerAttribute(mult.Attribute) * mult.Amount);
        }
    }
}
