using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The proficiency-bonus → battle-attribute-modifier conversion (spike #982 area E): a level's authored
    /// <see cref="ProficiencyModifier"/> payouts become <see cref="AttributeModifier"/>s (Proficiency source),
    /// and a player's total bonus is the cumulative set of every authored payout level at or below their
    /// current level.
    /// </summary>
    public class ProficiencyModifiersTests
    {
        [Fact]
        public void ToAttributeModifier_MapsFieldsAndStampsProficiencySource()
        {
            var modifier = new ProficiencyModifier
            {
                Attribute = EAttribute.Strength,
                ModifierType = EModifierType.Multiplicative,
                Amount = 1.5,
            };

            var result = modifier.ToAttributeModifier();

            Assert.Equal(EAttribute.Strength, result.Attribute);
            Assert.Equal(1.5, result.Amount);
            Assert.Equal(EModifierType.Multiplicative, result.Type);
            Assert.Equal(EAttributeModifierSource.Proficiency, result.Source);
        }

        [Fact]
        public void ModifiersForLevel_SumsEveryAuthoredPayoutAtOrBelowTheLevel()
        {
            var proficiency = Make(
                (1, [Mod(EAttribute.Strength, 5)]),
                (2, [Mod(EAttribute.Strength, 3)]),
                (3, [Mod(EAttribute.Endurance, 10)]));

            // At level 2 the player has earned the level-1 and level-2 payouts, but not level 3.
            var modifiers = proficiency.ModifiersForLevel(2).ToList();

            Assert.Equal(2, modifiers.Count);
            Assert.All(modifiers, m => Assert.Equal(EAttributeModifierSource.Proficiency, m.Source));
            Assert.Equal([5d, 3d], modifiers.Select(m => m.Amount));
            Assert.All(modifiers, m => Assert.Equal(EAttribute.Strength, m.Attribute));
        }

        [Fact]
        public void ModifiersForLevel_BelowEveryPayout_YieldsNothing()
        {
            var proficiency = Make((3, [Mod(EAttribute.Strength, 5)]));

            Assert.Empty(proficiency.ModifiersForLevel(2));
        }

        [Fact]
        public void ModifiersForLevel_IncludesAPayoutAuthoredAtLevelZero()
        {
            var proficiency = Make((0, [Mod(EAttribute.Strength, 7)]));

            var modifier = Assert.Single(proficiency.ModifiersForLevel(0));
            Assert.Equal(7, modifier.Amount);
        }

        [Fact]
        public void ModifiersForLevel_PreservesEachPayoutsModifierType()
        {
            var proficiency = Make(
                (1, [Mod(EAttribute.Strength, 5, EModifierType.Additive)]),
                (2, [Mod(EAttribute.Strength, 1.5, EModifierType.Multiplicative)]));

            var modifiers = proficiency.ModifiersForLevel(2).ToList();

            Assert.Equal(EModifierType.Additive, modifiers[0].Type);
            Assert.Equal(EModifierType.Multiplicative, modifiers[1].Type);
        }

        private static ProficiencyModifier Mod(
            EAttribute attribute, double amount, EModifierType type = EModifierType.Additive) => new()
            {
                Attribute = attribute,
                ModifierType = type,
                Amount = amount,
            };

        private static Proficiency Make(params (int Level, ProficiencyModifier[] Modifiers)[] levels) => new()
        {
            Id = 0,
            Name = "Test",
            Description = string.Empty,
            PathId = 0,
            PathOrdinal = 0,
            MaxLevel = 10,
            BaseXp = 100,
            XpGrowth = 2,
            StartsUnlocked = true,
            SeedSkillId = null,
            PrerequisiteIds = [],
            Levels = levels.Select(l => new ProficiencyLevel
            {
                Level = l.Level,
                Modifiers = l.Modifiers,
                RewardSkillId = null,
            }).ToList(),
        };
    }
}
