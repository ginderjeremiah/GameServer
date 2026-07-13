using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Skills;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Unit tests for the combat rating engine (#1531, spike #1526):
    /// <see cref="CombatRating.Rate"/>, <see cref="CombatRating.Marginal"/>, and the
    /// <see cref="CombatRating.Classify"/> exhaustiveness contract. Server-only domain math with no
    /// frontend/backend parity surface, so unlike most battle-math suites this one is not mirrored on the
    /// frontend.
    /// </summary>
    public class CombatRatingTests
    {
        // ── Classify exhaustiveness (spike #1526 Decision 9) ────────────────

        [Theory]
        [MemberData(nameof(AllAttributes))]
        public void Classify_IsDefinedForEveryAttribute(EAttribute attribute)
        {
            Assert.True(Enum.IsDefined(CombatRating.Classify(attribute)));
        }

        public static IEnumerable<object[]> AllAttributes()
        {
            return Enum.GetValues<EAttribute>().Select(a => new object[] { a });
        }

        // ── Rate: degenerate guards ──────────────────────────────────────────

        [Fact]
        public void Rate_BattlerWithNoSkills_ReturnsFinitePositiveValue()
        {
            var battler = MakeBattler();

            var rate = CombatRating.Rate(battler, isPlayer: true);

            Assert.True(rate > 0);
            Assert.False(double.IsNaN(rate));
            Assert.False(double.IsInfinity(rate));
        }

        // ── Rate: survivability ──────────────────────────────────────────────

        [Fact]
        public void Rate_HigherMaxHealthAndToughness_RatesHigher()
        {
            var weak = MakeBattler();
            var tough = MakeBattler((Endurance, 50));

            Assert.True(CombatRating.Rate(tough, isPlayer: true) > CombatRating.Rate(weak, isPlayer: true));
        }

        // ── Rate: offense ─────────────────────────────────────────────────────

        [Fact]
        public void Rate_SkillWithDamage_RatesHigherThanNoSkills()
        {
            var noSkills = MakeBattler();
            var withSkill = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

            Assert.True(CombatRating.Rate(withSkill, isPlayer: true) > CombatRating.Rate(noSkills, isPlayer: true));
        }

        [Fact]
        public void Rate_DamageReflection_RaisesOffense()
        {
            var noReflect = MakeBattler();
            var reflecting = MakeBattler((DamageReflection, 0.5));

            Assert.True(CombatRating.Rate(reflecting, isPlayer: true) > CombatRating.Rate(noReflect, isPlayer: true));
        }

        [Fact]
        public void Rate_DoTEffect_ContributesOffenseEvenWithZeroBaseDamage()
        {
            var dotEffect = new SkillEffect
            {
                Id = 1,
                Target = ESkillEffectTarget.Opponent,
                AttributeId = BleedDamagePerSecond,
                ModifierType = EModifierType.Additive,
                Amount = 10,
                DurationMs = 5000,
                ScalingAttributeId = Strength,
                ScalingAmount = 0,
            };
            var noDot = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 0)]);
            var withDot = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 0, effects: [dotEffect])]);

            Assert.True(CombatRating.Rate(withDot, isPlayer: true) > CombatRating.Rate(noDot, isPlayer: true));
        }

        [Fact]
        public void Rate_CoreAttributeScaledDamageMultiplier_RisesWithTheScalingAttribute()
        {
            // Every seeded damage skill scales off a core attribute (Punch/Strike/Cleave → Strength, Focus →
            // Intellect) via DamageMultipliers, read directly off effectiveCaster — pins that the effective
            // caster snapshot exposes real core values rather than zeroing them.
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 10, multipliers:
            [
                new DamageMultiplier { Attribute = Strength, Amount = 2.0 },
            ]);
            var weak = MakeBattlerWithSkills([], [skill]);
            var strong = MakeBattlerWithSkills([(Strength, 50)], [skill]);

            Assert.True(CombatRating.Rate(strong, isPlayer: true) > CombatRating.Rate(weak, isPlayer: true));
        }

        [Fact]
        public void Rate_DoTEffectScaledByCoreAttribute_RisesWithTheScalingAttribute()
        {
            // A DoT effect's ScalingAttributeId is a core attribute in general (mirroring how the live engine
            // scales an effect's magnitude off the caster) — pins that the DoT term reads it correctly rather
            // than zeroing it.
            var dotEffect = new SkillEffect
            {
                Id = 1,
                Target = ESkillEffectTarget.Opponent,
                AttributeId = BleedDamagePerSecond,
                ModifierType = EModifierType.Additive,
                Amount = 0,
                DurationMs = 5000,
                ScalingAttributeId = Strength,
                ScalingAmount = 1.0,
            };
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 0, effects: [dotEffect]);
            var weak = MakeBattlerWithSkills([], [skill]);
            var strong = MakeBattlerWithSkills([(Strength, 50)], [skill]);

            Assert.True(CombatRating.Rate(strong, isPlayer: true) > CombatRating.Rate(weak, isPlayer: true));
        }

        // ── Rate: enemy asymmetry (crit/dodge/parry/riposte/execute gated off) ──

        [Fact]
        public void Rate_EnemyWithAuthoredCriticalChance_IsUnaffectedByCrit()
        {
            var withCrit = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50, criticalChance: 1.0)]);
            var withoutCrit = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50, criticalChance: 0.0)]);

            Assert.Equal(
                CombatRating.Rate(withoutCrit, isPlayer: false),
                CombatRating.Rate(withCrit, isPlayer: false), 6);
        }

        [Fact]
        public void Rate_PlayerWithAuthoredCriticalChance_RatesHigherThanEnemyEquivalent()
        {
            var asPlayer = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50, criticalChance: 1.0)]);
            var asEnemy = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50, criticalChance: 1.0)]);

            Assert.True(CombatRating.Rate(asPlayer, isPlayer: true) > CombatRating.Rate(asEnemy, isPlayer: false));
        }

        [Fact]
        public void Rate_EnemyWithAuthoredExecuteBonus_IsUnaffectedByExecute()
        {
            var withExecute = MakeBattlerWithSkills([(ExecuteBonus, 1.0)], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);
            var withoutExecute = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

            Assert.Equal(
                CombatRating.Rate(withoutExecute, isPlayer: false),
                CombatRating.Rate(withExecute, isPlayer: false), 6);
        }

        [Fact]
        public void Rate_PlayerWithAuthoredExecuteBonus_RatesHigherThanEnemyEquivalent()
        {
            var asPlayer = MakeBattlerWithSkills([(ExecuteBonus, 1.0)], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);
            var asEnemy = MakeBattlerWithSkills([(ExecuteBonus, 1.0)], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

            Assert.True(CombatRating.Rate(asPlayer, isPlayer: true) > CombatRating.Rate(asEnemy, isPlayer: false));
        }

        [Fact]
        public void Rate_RiposteOnlyAppliesToPlayer()
        {
            var counter = MakeSkill(cooldownMs: 1000, baseDamage: 20);
            var withCounter = MakeBattlerWithSkills([(ParryChance, 0.5)], [], counterSkill: counter);
            var withoutCounter = MakeBattlerWithSkills([(ParryChance, 0.5)], []);

            Assert.True(CombatRating.Rate(withCounter, isPlayer: true) > CombatRating.Rate(withoutCounter, isPlayer: true));
            Assert.Equal(
                CombatRating.Rate(withoutCounter, isPlayer: false),
                CombatRating.Rate(withCounter, isPlayer: false), 6);
        }

        // ── Marginal: finite-difference dead-stat detection (#1529) ────────

        [Fact]
        public void Marginal_LuckWithNoCritOrParryEnabler_IsZero()
        {
            // Luck only scales CriticalChanceMultiplier/ParryChanceMultiplier — both idle at 0 × mult = 0
            // with no fielded crit-authored skill or authored ParryChance, so more Luck rates identically.
            var battler = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

            var marginal = CombatRating.Marginal(battler, isPlayer: true, Luck, delta: 100);

            Assert.Equal(0, marginal, 6);
        }

        [Fact]
        public void Marginal_AgilityWithNoDodgeOrCadenceEnabler_IsZero()
        {
            // AGI is an opt-in amplifier post-#1426/#1524: its CooldownRecovery derivation was severed, leaving it
            // to feed only CooldownBonusMultiplier and DodgeChanceMultiplier — both 0 × mult = 0 with no fielded
            // cadence or dodge enabler. So with neither enabler AGI is dead exactly like Luck, and the marginal is
            // 0 (matching the dead-stat detector the enemy content lint #1529 relies on).
            var battler = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

            var marginal = CombatRating.Marginal(battler, isPlayer: true, Agility, delta: 100);

            Assert.Equal(0, marginal, 6);
        }

        [Fact]
        public void Marginal_AgilityWithCadenceEnabler_RaisesRating()
        {
            // A fielded CooldownBonus (the cadence enabler) lights AGI up: more Agility scales
            // CooldownBonusMultiplier, raising the effective charge rate
            // (CooldownRecovery + CooldownBonus × CooldownBonusMultiplier) and thus the offense rate — the
            // dormant-not-dead amplifier turning live once its enabler is present (spike #1426/#1527).
            var battler = MakeBattlerWithSkills([(CooldownBonus, 0.5)], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

            var marginal = CombatRating.Marginal(battler, isPlayer: true, Agility, delta: 100);

            Assert.True(marginal > 0);
        }

        [Fact]
        public void Marginal_EnduranceRaisesRating()
        {
            var battler = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

            var marginal = CombatRating.Marginal(battler, isPlayer: true, Endurance, delta: 10);

            Assert.True(marginal > 0);
        }

        [Fact]
        public void Marginal_StrengthOnADamageBuild_RaisesRating()
        {
            // A damage build's primary stat must be visible to the marginal — the exact defect a core-stripped
            // effective-caster snapshot would hide.
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 10, multipliers:
            [
                new DamageMultiplier { Attribute = Strength, Amount = 2.0 },
            ]);
            var battler = MakeBattlerWithSkills([(Strength, 20)], [skill]);

            var marginal = CombatRating.Marginal(battler, isPlayer: true, Strength, delta: 10);

            Assert.True(marginal > 0);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Battler MakeBattler(params (EAttribute Attribute, double Amount)[] attributes)
        {
            return MakeBattlerWithSkills(attributes, []);
        }

        private static Battler MakeBattlerWithSkills(
            (EAttribute Attribute, double Amount)[] attributes, IReadOnlyList<Skill> skills, Skill? counterSkill = null)
        {
            var modifiers = attributes
                .Select(a => new AttributeModifier
                {
                    Attribute = a.Attribute,
                    Amount = a.Amount,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.AttributeDistribution,
                })
                .ToList();

            return new Battler(new AttributeCollection(modifiers), skills, 1, counterSkill);
        }

        private static Skill MakeSkill(
            int cooldownMs, double baseDamage, double criticalChance = 0,
            List<SkillEffect>? effects = null, List<DamageMultiplier>? multipliers = null) => new()
            {
                Id = 1,
                Name = "Test Skill",
                Description = string.Empty,
                DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1.0 }],
                CooldownMs = cooldownMs,
                BaseDamage = baseDamage,
                CriticalChance = criticalChance,
                DamageMultipliers = multipliers ?? [],
                Effects = effects ?? [],
            };
    }
}
