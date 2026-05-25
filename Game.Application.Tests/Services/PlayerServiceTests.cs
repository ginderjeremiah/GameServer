using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Application.Tests.Fakes;
using Game.Core;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Application.Tests.Services
{
    [TestClass]
    public class PlayerServiceTests
    {
        // ── TryUpdateAttributes ──────────────────────────────────────────────

        [TestMethod]
        public async Task TryUpdateAttributes_ValidUpdate_ReturnsTrue()
        {
            var (service, repo) = MakeService();

            // Player has 6 unspent stat points (gained 6, used 0).
            var player = MakePlayer(statPointsGained: 6, statPointsUsed: 0);

            // Spend 3 on Strength.
            var updates = new List<SimpleAttributeUpdate>
            {
                new(EAttribute.Strength, Amount: 3),
            };

            var result = await service.TryUpdateAttributes(player, updates);

            Assert.IsTrue(result);
            // SavePlayer should have been called once after a successful update.
            Assert.AreEqual(1, repo.SavePlayerCallCount);
        }

        [TestMethod]
        public async Task TryUpdateAttributes_SpendMoreThanAvailable_ReturnsFalse()
        {
            var (service, repo) = MakeService();

            // Player has only 2 unspent points.
            var player = MakePlayer(statPointsGained: 2, statPointsUsed: 0);

            // Trying to spend 10.
            var updates = new List<SimpleAttributeUpdate>
            {
                new(EAttribute.Strength, Amount: 10),
            };

            var result = await service.TryUpdateAttributes(player, updates);

            Assert.IsFalse(result);
            // No persistence call when validation fails.
            Assert.AreEqual(0, repo.SavePlayerCallCount);
        }

        // ── EquipItem ───────────────────────────────────────────────────────

        [TestMethod]
        public async Task EquipItem_UnlockedItem_ReturnsTrue()
        {
            var (service, repo) = MakeService();
            var player = MakePlayer();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(player.Inventory, item);

            var result = await service.EquipItem(player, 1, EEquipmentSlot.AccessorySlot);

            Assert.IsTrue(result);
            Assert.AreEqual(1, repo.SavePlayerCallCount);
        }

        [TestMethod]
        public async Task EquipItem_NotUnlocked_ReturnsFalse()
        {
            var (service, repo) = MakeService();
            var player = MakePlayer();

            var result = await service.EquipItem(player, 999, EEquipmentSlot.AccessorySlot);

            Assert.IsFalse(result);
            Assert.AreEqual(0, repo.SavePlayerCallCount);
        }

        // ── UnequipItem ─────────────────────────────────────────────────────

        [TestMethod]
        public async Task UnequipItem_EquippedItem_ReturnsTrue()
        {
            var (service, repo) = MakeService();
            var player = MakePlayer();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(player.Inventory, item);
            player.Inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            var result = await service.UnequipItem(player, EEquipmentSlot.AccessorySlot);

            Assert.IsTrue(result);
            Assert.AreEqual(1, repo.SavePlayerCallCount);
        }

        [TestMethod]
        public async Task UnequipItem_EmptySlot_ReturnsFalse()
        {
            var (service, repo) = MakeService();
            var player = MakePlayer();

            var result = await service.UnequipItem(player, EEquipmentSlot.AccessorySlot);

            Assert.IsFalse(result);
            Assert.AreEqual(0, repo.SavePlayerCallCount);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (PlayerService service, FakePlayerRepository repo) MakeService()
        {
            var repo = new FakePlayerRepository();
            var itemMods = new FakeItemMods();
            var service = new PlayerService(repo, itemMods);
            return (service, repo);
        }

        private static Player MakePlayer(int statPointsGained = 0, int statPointsUsed = 0)
        {
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,  Amount = 0 },
                new() { Attribute = EAttribute.Endurance, Amount = 0 },
                new() { Attribute = EAttribute.Intellect, Amount = 0 },
                new() { Attribute = EAttribute.Agility,   Amount = 0 },
                new() { Attribute = EAttribute.Dexterity, Amount = 0 },
                new() { Attribute = EAttribute.Luck,      Amount = 0 },
            };
            return new Player
            {
                Id = 1,
                Name = "Test",
                Level = 1,
                Exp = 0,
                CurrentZoneId = 0,
                StatPoints = new PlayerStatPoints(allocations)
                    { StatPointsGained = statPointsGained, StatPointsUsed = statPointsUsed },
                Inventory = new Inventory(),
                SelectedSkills = [],
                Skills = [],
                LogPreferences = [],
            };
        }

        private static Core.Items.Item MakeItem(int id, EItemCategory category = EItemCategory.Accessory) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = category,
            Attributes = [],
            ModSlots = [],
            Tags = [],
        };

        private static void AddUnlockedItem(Inventory inventory, Core.Items.Item item)
        {
            inventory.UnlockedItems.Add(new UnlockedItemSlot
            {
                ItemId = item.Id,
                Item = item,
                AppliedMods = [],
            });
        }

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;

        /// <summary>Minimal fake for <see cref="IItemMods"/> in tests.</summary>
        private class FakeItemMods : IItemMods
        {
            public void InvalidateCache() { }
            public List<Game.Abstractions.Entities.ItemMod> All(bool refreshCache = false) => [];
            public Dictionary<int, IEnumerable<Game.Abstractions.Entities.ItemMod>> GetModsForItemByType(int itemId) => [];
            public Game.Abstractions.Entities.ItemMod? GetItemMod(int itemModId) => null;
        }
    }
}
