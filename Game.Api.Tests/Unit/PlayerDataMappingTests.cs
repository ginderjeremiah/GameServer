using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using Xunit;
using CorePlayer = Game.Core.Players.Player;
using PlayerDataModel = Game.Api.Models.Player.PlayerData;

namespace Game.Api.Tests.Unit
{
    public class PlayerDataMappingTests
    {
        [Fact]
        public void FromPlayer_IncludesEveryUnlockedSkill_WithSelectedStateAndLoadoutOrder()
        {
            var skill5 = MakeSkill(5);
            var skill6 = MakeSkill(6);
            var skill7 = MakeSkill(7);
            var skill8 = MakeSkill(8);

            // SelectedSkills arrives already ordered (PlayerMapper.ToCore), so its index is the loadout
            // order: skill 7 is first (order 0), skill 5 second (order 1); 6 and 8 are unequipped.
            var player = MakePlayer(
                skills: [skill5, skill6, skill7, skill8],
                selectedSkills: [skill7, skill5]);

            var data = PlayerDataModel.FromPlayer(player);

            Assert.Equal(4, data.UnlockedSkills.Count);

            var equipped7 = data.UnlockedSkills.Single(s => s.SkillId == 7);
            Assert.True(equipped7.Selected);
            Assert.Equal(0, equipped7.Order);

            var equipped5 = data.UnlockedSkills.Single(s => s.SkillId == 5);
            Assert.True(equipped5.Selected);
            Assert.Equal(1, equipped5.Order);

            var unequipped6 = data.UnlockedSkills.Single(s => s.SkillId == 6);
            Assert.False(unequipped6.Selected);
            Assert.Null(unequipped6.Order);

            var unequipped8 = data.UnlockedSkills.Single(s => s.SkillId == 8);
            Assert.False(unequipped8.Selected);
            Assert.Null(unequipped8.Order);
        }

        [Fact]
        public void FromPlayer_NoUnlockedSkills_ReturnsEmptySet()
        {
            var player = MakePlayer(skills: [], selectedSkills: []);

            var data = PlayerDataModel.FromPlayer(player);

            Assert.Empty(data.UnlockedSkills);
        }

        private static CorePlayer MakePlayer(List<Skill> skills, List<Skill> selectedSkills) => new()
        {
            Id = 1,
            Name = "Test",
            Level = 1,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([]) { StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            Skills = skills,
            SelectedSkills = selectedSkills,
            LogPreferences = [],
        };

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            BaseDamage = 1,
            Description = string.Empty,
            CooldownMs = 1000,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
