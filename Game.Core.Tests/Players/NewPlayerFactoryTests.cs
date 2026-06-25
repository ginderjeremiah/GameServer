using Game.Core.Attributes;
using Game.Core.Classes;
using Game.Core.Players;
using Xunit;
using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Core.Tests.Players
{
    public class NewPlayerFactoryTests
    {
        private readonly NewPlayerFactory _factory = new();

        /// <summary>Builds a minimal class for creation: the given starter skills and attribute distribution,
        /// with a no-op signature passive and no starter equipment (consumed by later sub-issues).</summary>
        private static Class CreateClass(
            int id = 0,
            IReadOnlyList<int>? starterSkillIds = null,
            IReadOnlyList<AttributeDistribution>? attributeDistributions = null)
        {
            return new Class
            {
                Id = id,
                Name = "Test Class",
                StarterSkillIds = starterSkillIds ?? [0, 1, 2],
                StarterEquipment = [],
                AttributeDistributions = attributeDistributions ?? [],
                SignaturePassive = new ClassSignaturePassive
                {
                    Attribute = EAttribute.Strength,
                    Amount = 0m,
                    ScalingAttribute = null,
                    ScalingAmount = 0m,
                    ModifierType = EModifierType.Additive,
                },
            };
        }

        private static AttributeDistribution Distribution(EAttribute attribute, decimal baseAmount, decimal amountPerLevel = 0m) =>
            new() { AttributeId = attribute, BaseAmount = baseAmount, AmountPerLevel = amountPerLevel };

        [Fact]
        public void Create_UsesGivenNameClassAndStartingProgress()
        {
            var newPlayer = _factory.Create("hero", CreateClass(id: 3));

            Assert.Equal("hero", newPlayer.Name);
            Assert.Equal(3, newPlayer.ClassId);
            Assert.Equal(1, newPlayer.Level);
            Assert.Equal(0, newPlayer.Exp);
            Assert.Equal(NewPlayerFactory.StartingZoneId, newPlayer.CurrentZoneId);
            Assert.Equal(0, newPlayer.StatPointsGained);
            Assert.Equal(0, newPlayer.StatPointsUsed);
        }

        [Fact]
        public void Create_GrantsTheClassStarterSkillsAllSelectedInOrder()
        {
            var newPlayer = _factory.Create("hero", CreateClass(starterSkillIds: [4, 7, 2]));

            Assert.Equal([4, 7, 2], newPlayer.Skills.Select(skill => skill.SkillId));
            Assert.All(newPlayer.Skills, skill => Assert.True(skill.Selected));
            // Loadout order follows the authored kit order, sequential from 0.
            Assert.Equal([0, 1, 2], newPlayer.Skills.Select(skill => skill.Order));
        }

        [Fact]
        public void Create_DropsDuplicateStarterSkillIds()
        {
            // The shared path-less "punch" is content listed in every class's kit, so a kit may repeat a skill;
            // it is granted once (first wins) rather than producing a duplicate skill row.
            var newPlayer = _factory.Create("hero", CreateClass(starterSkillIds: [5, 9, 5]));

            Assert.Equal([5, 9], newPlayer.Skills.Select(skill => skill.SkillId));
            Assert.Equal([0, 1], newPlayer.Skills.Select(skill => skill.Order));
        }

        [Fact]
        public void Create_GrantsAnAllocationRowForEveryCoreAttribute_FromClassBaseSpread()
        {
            // The class invests a base spread into Strength/Endurance; the remaining core attributes get a row
            // at 0 (a row is required for each so PlayerStatPoints can later allocate into it).
            var newPlayer = _factory.Create("hero", CreateClass(attributeDistributions:
            [
                Distribution(EAttribute.Strength, 8m, amountPerLevel: 2m),
                Distribution(EAttribute.Endurance, 5m),
            ]));

            var expectedCoreAttributes = Enum.GetValues<EAttribute>().Where(CoreAttribute.IsCore).ToList();
            Assert.Equal(expectedCoreAttributes, newPlayer.Attributes.Select(attribute => attribute.Attribute));

            var amountByAttribute = newPlayer.Attributes.ToDictionary(a => a.Attribute, a => a.Amount);
            // The base spread is taken from BaseAmount only (level scaling/locked base is #1223), so AmountPerLevel
            // does not inflate the starting allocation.
            Assert.Equal(8d, amountByAttribute[EAttribute.Strength]);
            Assert.Equal(5d, amountByAttribute[EAttribute.Endurance]);
            // Every other core attribute is seeded at 0.
            Assert.All(
                amountByAttribute.Where(kvp => kvp.Key is not (EAttribute.Strength or EAttribute.Endurance)),
                kvp => Assert.Equal(0d, kvp.Value));
        }

        [Fact]
        public void Create_SetsDefaultLogPreferences()
        {
            var newPlayer = _factory.Create("hero", CreateClass());

            var enabledByType = newPlayer.LogPreferences.ToDictionary(
                preference => preference.LogType,
                preference => preference.Enabled);

            Assert.Equal(8, enabledByType.Count);
            Assert.False(enabledByType[ELogType.Damage]);
            Assert.False(enabledByType[ELogType.Debug]);
            Assert.True(enabledByType[ELogType.Exp]);
            Assert.True(enabledByType[ELogType.LevelUp]);
            Assert.True(enabledByType[ELogType.ItemFound]);
            Assert.True(enabledByType[ELogType.EnemyDefeated]);
            Assert.True(enabledByType[ELogType.SkillEffect]);
            Assert.True(enabledByType[ELogType.Proficiency]);
        }
    }
}
