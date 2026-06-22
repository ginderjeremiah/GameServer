using Game.Core;
using Game.Core.Items;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
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

            // SelectedSkills arrives already ordered (PlayerCacheMapper.ToCore), so its index is the loadout
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

        [Fact]
        public void FromPlayer_ProjectsUnlockedItems_WithEquippedStateAndSlot()
        {
            var weapon = MakeItem(10, EItemCategory.Weapon);
            var helm = MakeItem(11, EItemCategory.Helm);

            var inventory = new Inventory();
            inventory.UnlockItem(weapon);
            inventory.UnlockItem(helm);
            inventory.TryEquipItem(weapon.Id, EEquipmentSlot.WeaponSlot);

            var player = MakePlayer(skills: [], selectedSkills: [], inventory: inventory);

            var data = PlayerDataModel.FromPlayer(player);

            Assert.Equal(2, data.InventoryData.UnlockedItems.Count);

            var equippedWeapon = data.InventoryData.UnlockedItems.Single(i => i.ItemId == weapon.Id);
            Assert.True(equippedWeapon.Equipped);
            Assert.Equal((int)EEquipmentSlot.WeaponSlot, equippedWeapon.EquipmentSlotId);

            var unequippedHelm = data.InventoryData.UnlockedItems.Single(i => i.ItemId == helm.Id);
            Assert.False(unequippedHelm.Equipped);
            Assert.Null(unequippedHelm.EquipmentSlotId);
        }

        private static CorePlayer MakePlayer(List<Skill> skills, List<Skill> selectedSkills, Inventory? inventory = null)
        {
            var builder = new PlayerBuilder().WithSkills(skills).WithSelectedSkills(selectedSkills);
            if (inventory is not null)
            {
                builder.WithInventory(inventory);
            }
            return builder.Build();
        }

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            BaseDamage = 1,
            Description = string.Empty,
            Rarity = ERarity.Common,
            CooldownMs = 1000,
            DamageMultipliers = [],
            Effects = [],
        };

        private static Item MakeItem(int id, EItemCategory category) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = category,
            Rarity = ERarity.Common,
            Attributes = [],
            ModSlots = [],
        };
    }
}
