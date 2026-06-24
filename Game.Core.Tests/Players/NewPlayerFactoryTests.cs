using Game.Core.Players;
using Xunit;
using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Core.Tests.Players
{
    public class NewPlayerFactoryTests
    {
        private readonly NewPlayerFactory _factory = new();

        [Fact]
        public void Create_UsesGivenNameAndStartingProgress()
        {
            var newPlayer = _factory.Create("hero", []);

            Assert.Equal("hero", newPlayer.Name);
            Assert.Equal(1, newPlayer.Level);
            Assert.Equal(0, newPlayer.Exp);
            Assert.Equal(NewPlayerFactory.StartingZoneId, newPlayer.CurrentZoneId);
            Assert.Equal(0, newPlayer.StatPointsGained);
            Assert.Equal(0, newPlayer.StatPointsUsed);
        }

        [Fact]
        public void Create_GrantsStarterSkillsAllSelected()
        {
            var newPlayer = _factory.Create("hero", []);

            Assert.Equal(NewPlayerFactory.StarterSkillCount, newPlayer.Skills.Count);
            Assert.Equal(
                Enumerable.Range(0, NewPlayerFactory.StarterSkillCount),
                newPlayer.Skills.Select(skill => skill.SkillId));
            Assert.All(newPlayer.Skills, skill => Assert.True(skill.Selected));
        }

        [Fact]
        public void Create_AssignsSequentialLoadoutOrderToStarterSkills()
        {
            var newPlayer = _factory.Create("hero", []);

            Assert.Equal(
                Enumerable.Range(0, NewPlayerFactory.StarterSkillCount),
                newPlayer.Skills.Select(skill => skill.Order));
        }

        [Fact]
        public void Create_AppendsRootSeedSkillsUnselectedAfterTheStarterSkills()
        {
            // A tree-seeded root grants its native skill so the root is trainable from creation. Seeds land
            // unselected (earning a skill does not equip it) and ordered after the starter skills.
            var newPlayer = _factory.Create("hero", [7, 9]);

            Assert.Equal(NewPlayerFactory.StarterSkillCount + 2, newPlayer.Skills.Count);
            var seeds = newPlayer.Skills.Where(skill => !skill.Selected).ToList();
            Assert.Equal([7, 9], seeds.Select(skill => skill.SkillId));
            Assert.Equal(
                [NewPlayerFactory.StarterSkillCount, NewPlayerFactory.StarterSkillCount + 1],
                seeds.Select(skill => skill.Order));
        }

        [Fact]
        public void Create_DropsRootSeedSkillsAlreadyCoveredByAStarterSkill()
        {
            // Starter skills are ids 0..N-1; a seed id within that range is already granted, so it is dropped
            // rather than duplicated, and a duplicate seed id is granted once.
            var newPlayer = _factory.Create("hero", [0, 9, 9]);

            Assert.Equal(NewPlayerFactory.StarterSkillCount + 1, newPlayer.Skills.Count);
            var seed = Assert.Single(newPlayer.Skills, skill => !skill.Selected);
            Assert.Equal(9, seed.SkillId);
        }

        [Fact]
        public void Create_GrantsAnAllocationRowForEveryCoreAttribute()
        {
            var newPlayer = _factory.Create("hero", []);

            // The seed set is exactly the core (directly-allocatable) attributes — derived from the
            // attribute taxonomy, not a hardcoded count — so a new core attribute is granted automatically.
            var expectedCoreAttributes = Enum.GetValues<EAttribute>().Where(CoreAttribute.IsCore).ToList();
            Assert.Equal(expectedCoreAttributes, newPlayer.Attributes.Select(attribute => attribute.Attribute));
            Assert.All(newPlayer.Attributes, attribute =>
                Assert.Equal(NewPlayerFactory.StartingAttributeAmount, attribute.Amount));
        }

        [Fact]
        public void Create_SetsDefaultLogPreferences()
        {
            var newPlayer = _factory.Create("hero", []);

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
