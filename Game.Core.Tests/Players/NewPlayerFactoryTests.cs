using Game.Core.Players;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class NewPlayerFactoryTests
    {
        private readonly NewPlayerFactory _factory = new();

        [Fact]
        public void Create_UsesGivenNameAndStartingProgress()
        {
            var newPlayer = _factory.Create("hero");

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
            var newPlayer = _factory.Create("hero");

            Assert.Equal(NewPlayerFactory.StarterSkillCount, newPlayer.Skills.Count);
            Assert.Equal(
                Enumerable.Range(0, NewPlayerFactory.StarterSkillCount),
                newPlayer.Skills.Select(skill => skill.SkillId));
            Assert.All(newPlayer.Skills, skill => Assert.True(skill.Selected));
        }

        [Fact]
        public void Create_AssignsSequentialLoadoutOrderToStarterSkills()
        {
            var newPlayer = _factory.Create("hero");

            Assert.Equal(
                Enumerable.Range(0, NewPlayerFactory.StarterSkillCount),
                newPlayer.Skills.Select(skill => skill.Order));
        }

        [Fact]
        public void Create_GrantsEachCoreAttributeAtStartingAmount()
        {
            var newPlayer = _factory.Create("hero");

            Assert.Equal(NewPlayerFactory.AttributeCount, newPlayer.Attributes.Count);
            Assert.Equal(
                Enumerable.Range(0, NewPlayerFactory.AttributeCount).Select(id => (EAttribute)id),
                newPlayer.Attributes.Select(attribute => attribute.Attribute));
            Assert.All(newPlayer.Attributes, attribute =>
                Assert.Equal(NewPlayerFactory.StartingAttributeAmount, attribute.Amount));
        }

        [Fact]
        public void Create_SetsDefaultLogPreferences()
        {
            var newPlayer = _factory.Create("hero");

            var enabledByType = newPlayer.LogPreferences.ToDictionary(
                preference => preference.LogType,
                preference => preference.Enabled);

            Assert.Equal(7, enabledByType.Count);
            Assert.False(enabledByType[ELogType.Damage]);
            Assert.False(enabledByType[ELogType.Debug]);
            Assert.True(enabledByType[ELogType.Exp]);
            Assert.True(enabledByType[ELogType.LevelUp]);
            Assert.True(enabledByType[ELogType.ItemFound]);
            Assert.True(enabledByType[ELogType.EnemyDefeated]);
            Assert.True(enabledByType[ELogType.SkillEffect]);
        }
    }
}
