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
                // Damage is computed and dealt from the pre-effect attribute state, then the effects are
                // applied — so a self damage-buff never boosts the hit that carries it, and an earlier
                // loadout slot's effect does influence a later slot firing on the same tick.
                var damage = CalculateDamage(context);
                // Record the actual post-crit/post-mitigation damage DamageTarget returns (the sum across the
                // skill's weighted damage portions, #1343), so per-skill stats reconcile with the global stats
                // (which DamageTarget also books) rather than the raw pre-mitigation value. Recording therefore
                // has to follow the hit, not precede it.
                var actualDamage = context.DamageTarget(damage, Skill.DamagePortions, Skill.CriticalChance);
                context.RecordSkillUse(Skill.Id, actualDamage);
                ApplyEffects(context);
            }
        }

        private void ApplyEffects(BattleContext context)
        {
            // Index loop, not foreach: Skill.Effects is an IReadOnlyList<> (#547), whose foreach would box an
            // interface enumerator on every skill fire — the same per-fire allocation the hot path avoids below.
            var effects = Skill.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                context.ApplySkillEffect(effects[i]);
            }
        }

        public double CalculateDamage(BattleContext context)
        {
            return context.CalculateRawDamage(Skill);
        }
    }
}
