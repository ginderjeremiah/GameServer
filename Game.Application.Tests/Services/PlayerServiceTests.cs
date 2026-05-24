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
            var repo = new FakePlayerRepository();
            var service = new PlayerService(repo);

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
            var repo = new FakePlayerRepository();
            var service = new PlayerService(repo);

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

        // ── UpdateInventorySlots ─────────────────────────────────────────────

        [TestMethod]
        public async Task UpdateInventorySlots_ValidReorder_ReturnsTrue()
        {
            var repo = new FakePlayerRepository();
            var service = new PlayerService(repo);

            var player = MakePlayer();
            var item1 = MakeItem(1);
            var item2 = MakeItem(2);
            player.Inventory.TryAddItem(item1, inventoryItemId: 10); // slot 0
            player.Inventory.TryAddItem(item2, inventoryItemId: 11); // slot 1

            // Swap the two items.
            var updates = new List<SimpleInventoryUpdate>
            {
                new(Id: 10, SlotNumber: 1, Equipped: false),
                new(Id: 11, SlotNumber: 0, Equipped: false),
            };

            var result = await service.UpdateInventorySlots(player, updates);

            Assert.IsTrue(result);
            var slot0Item = player.Inventory.InventorySlots.First(s => s.SlotNumber == 0).Item;
            var slot1Item = player.Inventory.InventorySlots.First(s => s.SlotNumber == 1).Item;
            Assert.AreEqual(item2, slot0Item);
            Assert.AreEqual(item1, slot1Item);
        }

        [TestMethod]
        public async Task UpdateInventorySlots_DuplicateSlot_ReturnsFalse()
        {
            var repo = new FakePlayerRepository();
            var service = new PlayerService(repo);

            var player = MakePlayer();
            player.Inventory.TryAddItem(MakeItem(1), inventoryItemId: 10);
            player.Inventory.TryAddItem(MakeItem(2), inventoryItemId: 11);

            // Both items targeting slot 0 — invalid.
            var updates = new List<SimpleInventoryUpdate>
            {
                new(Id: 10, SlotNumber: 0, Equipped: false),
                new(Id: 11, SlotNumber: 0, Equipped: false),
            };

            var result = await service.UpdateInventorySlots(player, updates);

            Assert.IsFalse(result);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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
            };
        }

        private static Item MakeItem(int id) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = EItemCategory.Accessory,
            Attributes = [],
            ModSlots = [],
            Tags = [],
        };

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;

        private record SimpleInventoryUpdate(int Id, int SlotNumber, bool Equipped) : IInventoryUpdate;
    }
}
