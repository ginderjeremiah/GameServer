using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Skills;
using Xunit;
using static Game.Core.EAttribute;
using static Game.Core.EModifierType;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Unit coverage for the timed skill-effect bookkeeping on <see cref="Battler"/>,
    /// <see cref="BattleSkill"/> and <see cref="BattleContext"/>. Mirrors the frontend suite
    /// <c>UI/src/tests/lib/battle/battler-effects.test.ts</c> — identical scenarios and expected
    /// values on both sides of the battle-parity boundary.
    /// </summary>
    public class BattlerEffectsTests
    {
        [Fact]
        public void ApplyEffect_AddsModifier_RaisesAttribute()
        {
            var battler = MakeBattler(Stat(Strength, 10));

            Apply(battler, Effect(1, Strength, Additive, 5));

            Assert.Equal(15, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void ApplyEffect_SameEffectTwice_Stacks()
        {
            var battler = MakeBattler(Stat(Strength, 10));
            var effect = Effect(1, Strength, Additive, 5);

            Apply(battler, effect);
            Apply(battler, effect);

            // Each application adds its own modifier, so re-applying the same authored effect stacks the
            // magnitude (20 = 10 base + 5 + 5).
            Assert.Equal(20, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void ApplyEffect_DifferentEffectsSameAttribute_Stack()
        {
            var battler = MakeBattler(Stat(Strength, 10));

            Apply(battler, Effect(1, Strength, Additive, 5));
            Apply(battler, Effect(2, Strength, Additive, 3));

            Assert.Equal(18, battler.GetAttributeValue(Strength));
        }

        [Theory]
        [InlineData(40, 1)]   // DurationMs / 40 = 1 tick influenced (the application tick).
        [InlineData(80, 2)]
        [InlineData(120, 3)]
        public void AdvanceEffects_RemovesEffect_AfterItsDurationInTicks(int durationMs, int influencedTicks)
        {
            var battler = MakeBattler(Stat(Strength, 10));
            Apply(battler, Effect(1, Strength, Additive, 5, durationMs));
            Assert.Equal(15, battler.GetAttributeValue(Strength));

            // The effect remains for (influencedTicks - 1) further ticks after the one it was applied on.
            for (var tick = 1; tick < influencedTicks; tick++)
            {
                battler.AdvanceEffects(40);
                Assert.Equal(15, battler.GetAttributeValue(Strength));
            }

            // The next tick takes its remaining duration to zero, removing the modifier.
            battler.AdvanceEffects(40);
            Assert.Equal(10, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void AdvanceEffects_StackedApplications_ShareResetExpiration_ExtendingTheOlderOne()
        {
            var battler = MakeBattler(Stat(Strength, 10));
            var effect = Effect(1, Strength, Additive, 5, durationMs: 80);

            Apply(battler, effect); // application A: expires at 80
            Assert.Equal(15, battler.GetAttributeValue(Strength));

            battler.AdvanceEffects(40);  // elapsed 40, A still active
            Apply(battler, effect); // application B at elapsed 40: resets the shared expiry to 40 + 80 = 120
            Assert.Equal(20, battler.GetAttributeValue(Strength)); // both stacked

            // Independent expirations would drop A here (its original expiry was 80); the shared reset to 120
            // extends it, so both remain.
            battler.AdvanceEffects(40); // elapsed 80
            Assert.Equal(20, battler.GetAttributeValue(Strength));

            battler.AdvanceEffects(40); // elapsed 120 → the whole stack expires together
            Assert.Equal(10, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void AdvanceEffects_ReapplyingShorterEffectOnSameAttribute_CutsTheStackShort()
        {
            var battler = MakeBattler(Stat(Strength, 10));

            // A long application (5 ticks), then a different, shorter effect (1 tick) on the SAME attribute.
            Apply(battler, Effect(1, Strength, Additive, 5, durationMs: 200)); // A: expires at 200
            battler.AdvanceEffects(40); // elapsed 40
            Apply(battler, Effect(2, Strength, Additive, 3, durationMs: 40)); // B at elapsed 40: resets shared expiry to 80
            Assert.Equal(18, battler.GetAttributeValue(Strength)); // 10 + 5 + 3 (both stacked)

            // The new (shorter) application sets the shared expiry, cutting A short from 200 to 80, so the
            // whole stack expires together at elapsed 80.
            battler.AdvanceEffects(40); // elapsed 80
            Assert.Equal(10, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void AdvanceEffects_EffectsOnDifferentAttributes_ExpireIndependently()
        {
            var battler = MakeBattler(Stat(Strength, 10), Stat(Agility, 10));

            // The shared expiry is per-attribute, so a second effect on a DIFFERENT attribute neither stacks
            // with nor resets the first.
            Apply(battler, Effect(1, Strength, Additive, 5, durationMs: 200)); // Strength: expires at 200
            battler.AdvanceEffects(40); // elapsed 40
            Apply(battler, Effect(2, Agility, Additive, 3, durationMs: 40)); // Agility: expires at 80, leaves Strength alone

            battler.AdvanceEffects(40); // elapsed 80 → Agility expires, Strength (expiry 200) remains
            Assert.Equal(15, battler.GetAttributeValue(Strength));
            Assert.Equal(10, battler.GetAttributeValue(Agility));
        }

        [Fact]
        public void ApplyEffect_StrengthBuff_RaisesMaxHealthViaDerivedCascade_WithoutHealing()
        {
            var battler = MakeBattler(Stat(Strength, 10)); // MaxHealth = 50 + 5*10 = 100
            Assert.Equal(100, battler.GetAttributeValue(MaxHealth));
            Assert.Equal(100, battler.CurrentHealth);

            Apply(battler, Effect(1, Strength, Additive, 10)); // Str 10 → 20

            Assert.Equal(20, battler.GetAttributeValue(Strength));
            Assert.Equal(150, battler.GetAttributeValue(MaxHealth)); // 50 + 5*20
            Assert.Equal(100, battler.CurrentHealth); // a rise in MaxHealth never heals
        }

        [Fact]
        public void ApplyEffect_MaxHealthDebuff_ClampsCurrentHealthDown()
        {
            var battler = MakeBattler(Stat(Strength, 10)); // MaxHealth = CurrentHealth = 100

            Apply(battler, Effect(1, MaxHealth, Multiplicative, 0.5));

            Assert.Equal(50, battler.GetAttributeValue(MaxHealth));
            Assert.Equal(50, battler.CurrentHealth); // clamped down to the new max
        }

        [Fact]
        public void AdvanceEffects_BuffExpiry_ClampsCurrentHealthToDroppedMax()
        {
            var battler = MakeBattler(Stat(Strength, 10)); // base MaxHealth = 50 + 5*10 = 100

            // A Strength buff (1 tick) lifts MaxHealth to (50 + 5*30) = 200 via the derived cascade; a direct
            // ×0.5 MaxHealth (long) halves it back to 100, so CurrentHealth (100) is not clamped while both
            // are active. They sit on DIFFERENT attributes (Strength vs MaxHealth), so they expire
            // independently — the Strength buff can lapse while the MaxHealth debuff persists.
            Apply(battler, Effect(1, Strength, Additive, 20, durationMs: 40));
            Apply(battler, Effect(2, MaxHealth, Multiplicative, 0.5, durationMs: 1000));
            Assert.Equal(100, battler.GetAttributeValue(MaxHealth));
            Assert.Equal(100, battler.CurrentHealth);

            // The Strength buff expires: MaxHealth drops to (50 + 5*10) * 0.5 = 50, dragging CurrentHealth down.
            battler.AdvanceEffects(40);
            Assert.Equal(50, battler.GetAttributeValue(MaxHealth));
            Assert.Equal(50, battler.CurrentHealth);
        }

        [Fact]
        public void BattleContext_ApplySkillEffect_RoutesSelfToActiveAndOpponentToTarget()
        {
            var player = MakeBattler(Stat(Strength, 10));
            var enemy = MakeBattler(Stat(Strength, 10));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ApplySkillEffect(Effect(1, Strength, Additive, 5, target: ESkillEffectTarget.Self));
            context.ApplySkillEffect(Effect(2, Strength, Additive, 7, target: ESkillEffectTarget.Opponent));

            Assert.Equal(15, player.GetAttributeValue(Strength));
            Assert.Equal(17, enemy.GetAttributeValue(Strength));
        }

        [Fact]
        public void BattleContext_ApplySkillEffect_ScalesAmountWithCasterAttribute()
        {
            // The issue's example: a poison applied to the enemy whose magnitude scales with the caster's
            // Dexterity. Base 10 + Dexterity(20) × 0.5 = 20 DamageTakenPerSecond on the enemy.
            var player = MakeBattler(Stat(Dexterity, 20));
            var enemy = MakeBattler(Stat(Dexterity, 0));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ApplySkillEffect(Effect(1, DamageTakenPerSecond, Additive, 10,
                target: ESkillEffectTarget.Opponent, scalingAttribute: Dexterity, scalingAmount: 0.5));

            Assert.Equal(20, enemy.GetAttributeValue(DamageTakenPerSecond));
        }

        [Fact]
        public void BattleContext_ApplySkillEffect_ScalingReadsCasterNotTarget()
        {
            // An Opponent effect scaling with Dexterity reads the CASTER's Dexterity, not the target's, so a
            // high-Dexterity enemy target does not inflate a low-Dexterity caster's debuff.
            var player = MakeBattler(Stat(Dexterity, 4));   // caster
            var enemy = MakeBattler(Stat(Dexterity, 100));  // target
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ApplySkillEffect(Effect(1, DamageTakenPerSecond, Additive, 0,
                target: ESkillEffectTarget.Opponent, scalingAttribute: Dexterity, scalingAmount: 1.0));

            // 0 + caster Dexterity(4) × 1.0 = 4, not the target's 100 (DamageTakenPerSecond has base 0).
            Assert.Equal(4, enemy.GetAttributeValue(DamageTakenPerSecond));
        }

        [Fact]
        public void BattleContext_ApplySkillEffect_ZeroScalingAmount_LeavesAmountUnchanged()
        {
            var player = MakeBattler(Stat(Dexterity, 50));
            var enemy = MakeBattler(Stat(Dexterity, 0));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            // ScalingAmount 0 ⇒ the authored amount is used verbatim regardless of the caster's attribute.
            context.ApplySkillEffect(Effect(1, DamageTakenPerSecond, Additive, 7,
                target: ESkillEffectTarget.Opponent, scalingAttribute: Dexterity, scalingAmount: 0));

            Assert.Equal(7, enemy.GetAttributeValue(DamageTakenPerSecond));
        }

        [Fact]
        public void BattleSkill_Fire_DealsDamageBeforeApplyingItsSelfBuff()
        {
            var player = MakeBattler(Stat(Strength, 10));
            var enemy = MakeBattler(Stat(Strength, 10)); // Defense = 2
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            // A skill that scales with Strength and buffs the caster's Strength when it fires.
            var skill = new BattleSkill(new Skill
            {
                Id = 1,
                Name = "Buff Strike",
                Description = "",
                CooldownMs = 40,
                BaseDamage = 0,
                DamageMultipliers =
                [
                    new DamageMultiplier { Attribute = Strength, Amount = 1.0 },
                ],
                Effects = [Effect(1, Strength, Additive, 10, target: ESkillEffectTarget.Self)],
            });

            skill.Update(context); // charges 40 ≥ 40 cooldown → fires this tick

            // The carrying hit used pre-buff Strength (10): 10 - 2 defense = 8 dealt.
            Assert.Equal(92, enemy.CurrentHealth);
            // The self buff lands only after the hit, so the caster's Strength is now boosted for later hits.
            Assert.Equal(20, player.GetAttributeValue(Strength));
        }

        /// <summary>Applies an effect with its authored amount (no caster scaling), the common case these
        /// bookkeeping tests exercise. Scaling is computed by <see cref="BattleContext.ApplySkillEffect"/>
        /// and covered separately.</summary>
        private static void Apply(Battler battler, SkillEffect effect) => battler.ApplyEffect(effect, effect.Amount);

        private static Battler MakeBattler(params AttributeModifier[] modifiers) =>
            new(new AttributeCollection(modifiers), [], 1);

        private static AttributeModifier Stat(EAttribute attribute, double amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = Additive,
            Source = EAttributeModifierSource.PlayerStatPoints,
        };

        private static SkillEffect Effect(
            int id, EAttribute attribute, EModifierType type, double amount,
            int durationMs = 1000, ESkillEffectTarget target = ESkillEffectTarget.Self,
            EAttribute scalingAttribute = EAttribute.Strength, double scalingAmount = 0) => new()
            {
                Id = id,
                Target = target,
                AttributeId = attribute,
                ModifierType = type,
                Amount = amount,
                DurationMs = durationMs,
                ScalingAttributeId = scalingAttribute,
                ScalingAmount = scalingAmount,
            };
    }
}
