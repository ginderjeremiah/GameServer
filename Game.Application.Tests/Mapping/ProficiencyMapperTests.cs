using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="ProficiencyMapper"/>: the contract projection round-trips fields and child
    /// collections, and <c>ToCore</c> groups the flat per-level modifier/reward rows into one ascending
    /// <see cref="Core.Proficiencies.ProficiencyLevel"/> list (the only non-trivial mapping logic).
    /// </summary>
    public class ProficiencyMapperTests
    {
        [Fact]
        public void ToCore_GroupsModifiersAndRewardsByLevel_Ascending()
        {
            var entity = NewProficiency(
                modifiers:
                [
                    Modifier(level: 1, EAttribute.Strength, 1m),
                    Modifier(level: 1, EAttribute.Endurance, 2m),
                    Modifier(level: 5, EAttribute.Strength, 10m),
                ],
                rewards:
                [
                    Reward(level: 3, rewardSkillId: 9),
                    Reward(level: 5, rewardSkillId: 7),
                ]);

            var core = ProficiencyMapper.ToCore(entity);

            Assert.Equal([1, 3, 5], core.Levels.Select(l => l.Level));

            var level1 = core.Levels.Single(l => l.Level == 1);
            Assert.Equal(2, level1.Modifiers.Count);
            Assert.Contains(level1.Modifiers, m => m.Attribute == EAttribute.Strength);
            Assert.Contains(level1.Modifiers, m => m.Attribute == EAttribute.Endurance);
            Assert.Null(level1.RewardSkillId);

            var level3 = core.Levels.Single(l => l.Level == 3);
            Assert.Empty(level3.Modifiers);
            Assert.Equal(9, level3.RewardSkillId);

            var level5 = core.Levels.Single(l => l.Level == 5);
            Assert.Equal(EModifierType.Additive, level5.Modifiers.Single().ModifierType);
            Assert.Equal(10d, level5.Modifiers.Single().Amount);
            Assert.Equal(7, level5.RewardSkillId);
        }

        [Fact]
        public void ToCore_MapsScalarFieldsAndPrerequisites()
        {
            var entity = NewProficiency();
            entity.Prerequisites = [new() { ProficiencyId = 0, PrerequisiteProficiencyId = 2 }];

            var core = ProficiencyMapper.ToCore(entity);

            Assert.Equal(0, core.Id);
            Assert.Equal("Blades", core.Name);
            Assert.Equal(1, core.PathId);
            Assert.Equal(2, core.PathOrdinal);
            Assert.Equal(10, core.MaxLevel);
            Assert.Equal(100d, core.BaseXp);
            Assert.Equal(1.5d, core.XpGrowth);
            Assert.Equal([2], core.PrerequisiteIds);
        }

        [Fact]
        public void ToContract_RoundTripsFieldsAndCollections()
        {
            var entity = NewProficiency(
                modifiers: [Modifier(level: 2, EAttribute.Agility, 3m)],
                rewards: [Reward(level: 2, rewardSkillId: 8)]);

            var contract = ProficiencyMapper.ToContract(entity);

            Assert.Equal("Blades", contract.Name);
            Assert.Equal("designer intent", contract.DesignerNotes);
            Assert.Equal("aenkor", contract.Word);
            Assert.Equal("AYN-kor", contract.Pronunciation);
            Assert.Equal("The First Flame", contract.Translation);
            Assert.Equal(1, contract.PathId);
            Assert.Equal(2, contract.PathOrdinal);
            Assert.Equal(100m, contract.BaseXp);
            var modifier = Assert.Single(contract.LevelModifiers);
            Assert.Equal(EAttribute.Agility, modifier.AttributeId);
            Assert.Equal(3m, modifier.Amount);
            var reward = Assert.Single(contract.LevelRewards);
            Assert.Equal(8, reward.RewardSkillId);
        }

        [Fact]
        public void PathMapper_ToContract_RoundTripsActivityKey()
        {
            var path = new Entities.Path
            {
                Id = 0,
                Name = "Fire",
                Description = "The fire line.",
                ActivityKey = (int)EActivityKey.Fire,
                DesignerNotes = "designer intent",
            };

            var contract = PathMapper.ToContract(path);

            Assert.Equal("Fire", contract.Name);
            Assert.Equal(EActivityKey.Fire, contract.ActivityKey);
            Assert.Equal("designer intent", contract.DesignerNotes);
        }

        private static Entities.ProficiencyLevelModifier Modifier(int level, EAttribute attribute, decimal amount) => new()
        {
            ProficiencyId = 0,
            Level = level,
            AttributeId = (int)attribute,
            ModifierType = (int)EModifierType.Additive,
            Amount = amount,
        };

        private static Entities.ProficiencyLevelReward Reward(int level, int rewardSkillId) => new()
        {
            ProficiencyId = 0,
            Level = level,
            RewardSkillId = rewardSkillId,
        };

        private static Entities.Proficiency NewProficiency(
            List<Entities.ProficiencyLevelModifier>? modifiers = null,
            List<Entities.ProficiencyLevelReward>? rewards = null) => new()
            {
                Id = 0,
                Name = "Blades",
                Description = "A blade discipline.",
                IconPath = "blades.png",
                Word = "aenkor",
                Pronunciation = "AYN-kor",
                Translation = "The First Flame",
                PathId = 1,
                PathOrdinal = 2,
                MaxLevel = 10,
                BaseXp = 100m,
                XpGrowth = 1.5m,
                DesignerNotes = "designer intent",
                LevelModifiers = modifiers ?? [],
                LevelRewards = rewards ?? [],
                Prerequisites = [],
            };
    }
}
