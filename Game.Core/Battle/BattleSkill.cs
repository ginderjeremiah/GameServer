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
                // Record the actual post-crit/post-mitigation/block damage DamageTarget returns, so per-skill
                // stats reconcile with the global stats (which DamageTarget also books) rather than the raw
                // pre-mitigation value. Recording therefore has to follow the hit, not precede it.
                // Until the portion-aware pipeline (#1385) splits a hit across the skill's portions, the direct
                // hit deals its single PrimaryDamageType — identical behaviour, since every skill currently has
                // exactly one (Physical) portion.
                var actualDamage = context.DamageTarget(damage, Skill.PrimaryDamageType);
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
            // Accumulate the multiplier bonus separately and add BaseDamage last, exactly mirroring the
            // previous `BaseDamage + DamageMultipliers.Sum(...)`. Floating-point addition is not
            // associative, so preserving this order keeps the result bit-for-bit identical — required
            // for frontend/backend battle parity. The manual index loop avoids the per-fire allocations on
            // this hot path: the old LINQ Sum boxed a list enumerator plus a closure capturing `context`
            // (#286), and a foreach over the IReadOnlyList<> DamageMultipliers (#547) would itself box an
            // interface enumerator — indexing the list allocates nothing.
            var multiplierBonus = 0.0;
            var multipliers = Skill.DamageMultipliers;
            for (var i = 0; i < multipliers.Count; i++)
            {
                var multiplier = multipliers[i];
                multiplierBonus += context.GetActiveBattlerAttribute(multiplier.Attribute) * multiplier.Amount;
            }

            return Skill.BaseDamage + multiplierBonus;
        }
    }
}
