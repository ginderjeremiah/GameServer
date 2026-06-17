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

            battler.ApplyEffect(Effect(1, Strength, Additive, 5));

            Assert.Equal(15, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void ApplyEffect_SameEffectTwice_RefreshesWithoutStacking()
        {
            var battler = MakeBattler(Stat(Strength, 10));
            var effect = Effect(1, Strength, Additive, 5);

            battler.ApplyEffect(effect);
            battler.ApplyEffect(effect);

            // A second application of the same authored effect refreshes its duration rather than adding
            // a second modifier, so the magnitude does not stack (15, not 20).
            Assert.Equal(15, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void ApplyEffect_DifferentEffectsSameAttribute_Stack()
        {
            var battler = MakeBattler(Stat(Strength, 10));

            battler.ApplyEffect(Effect(1, Strength, Additive, 5));
            battler.ApplyEffect(Effect(2, Strength, Additive, 3));

            Assert.Equal(18, battler.GetAttributeValue(Strength));
        }

        [Theory]
        [InlineData(40, 1)]   // DurationMs / 40 = 1 tick influenced (the application tick).
        [InlineData(80, 2)]
        [InlineData(120, 3)]
        public void AdvanceEffects_RemovesEffect_AfterItsDurationInTicks(int durationMs, int influencedTicks)
        {
            var battler = MakeBattler(Stat(Strength, 10));
            battler.ApplyEffect(Effect(1, Strength, Additive, 5, durationMs));
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
        public void ApplyEffect_RefreshResetsRemainingDuration()
        {
            var battler = MakeBattler(Stat(Strength, 10));
            var effect = Effect(1, Strength, Additive, 5, durationMs: 80);

            battler.ApplyEffect(effect);
            battler.AdvanceEffects(40); // remaining 80 → 40
            battler.ApplyEffect(effect); // refresh remaining back to 80

            battler.AdvanceEffects(40); // 80 → 40, still active
            Assert.Equal(15, battler.GetAttributeValue(Strength));
            battler.AdvanceEffects(40); // 40 → 0, removed
            Assert.Equal(10, battler.GetAttributeValue(Strength));
        }

        [Fact]
        public void ApplyEffect_StrengthBuff_RaisesMaxHealthViaDerivedCascade_WithoutHealing()
        {
            var battler = MakeBattler(Stat(Strength, 10)); // MaxHealth = 50 + 5*10 = 100
            Assert.Equal(100, battler.GetAttributeValue(MaxHealth));
            Assert.Equal(100, battler.CurrentHealth);

            battler.ApplyEffect(Effect(1, Strength, Additive, 10)); // Str 10 → 20

            Assert.Equal(20, battler.GetAttributeValue(Strength));
            Assert.Equal(150, battler.GetAttributeValue(MaxHealth)); // 50 + 5*20
            Assert.Equal(100, battler.CurrentHealth); // a rise in MaxHealth never heals
        }

        [Fact]
        public void ApplyEffect_MaxHealthDebuff_ClampsCurrentHealthDown()
        {
            var battler = MakeBattler(Stat(Strength, 10)); // MaxHealth = CurrentHealth = 100

            battler.ApplyEffect(Effect(1, MaxHealth, Multiplicative, 0.5));

            Assert.Equal(50, battler.GetAttributeValue(MaxHealth));
            Assert.Equal(50, battler.CurrentHealth); // clamped down to the new max
        }

        [Fact]
        public void AdvanceEffects_BuffExpiry_ClampsCurrentHealthToDroppedMax()
        {
            var battler = MakeBattler(Stat(Strength, 10)); // base MaxHealth = 100

            // H (additive, 1 tick) lifts the additive subtotal to 200; L (×0.5, long) then halves it back to
            // 100, so CurrentHealth (100) is not clamped while both are active.
            battler.ApplyEffect(Effect(1, MaxHealth, Additive, 100, durationMs: 40));
            battler.ApplyEffect(Effect(2, MaxHealth, Multiplicative, 0.5, durationMs: 1000));
            Assert.Equal(100, battler.GetAttributeValue(MaxHealth));
            Assert.Equal(100, battler.CurrentHealth);

            // H expires: MaxHealth drops to 100 * 0.5 = 50, dragging CurrentHealth down with it.
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
            int durationMs = 1000, ESkillEffectTarget target = ESkillEffectTarget.Self) => new()
            {
                Id = id,
                Target = target,
                AttributeId = attribute,
                ModifierType = type,
                Amount = amount,
                DurationMs = durationMs,
            };
    }
}
