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

        // ── Rate: enemy asymmetry (crit/dodge/parry/riposte gated off) ──────

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
        public void Marginal_AgilityRaisesRating_EvenWithNoDodgeOrCadenceEnabler()
        {
            // AGI always feeds CooldownRecovery (a universal tempo identity, not an enabler-gated one), so
            // it is never fully dead the way Luck is above.
            var battler = MakeBattlerWithSkills([], [MakeSkill(cooldownMs: 1000, baseDamage: 50)]);

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
            int cooldownMs, double baseDamage, double criticalChance = 0, List<SkillEffect>? effects = null) => new()
            {
                Id = 1,
                Name = "Test Skill",
                Description = string.Empty,
                DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1.0 }],
                CooldownMs = cooldownMs,
                BaseDamage = baseDamage,
                CriticalChance = criticalChance,
                DamageMultipliers = [],
                Effects = effects ?? [],
            };
    }
}
