using Game.Core.Events;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Core.Tests.Players
{
    [TestClass]
    public class PlayerTests
    {
        // ── GrantExp ─────────────────────────────────────────────────────────

        [TestMethod]
        public void GrantExp_BelowLevelThreshold_DoesNotLevelUp()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(50); // threshold is 1 * 100 = 100

            Assert.AreEqual(1, player.Level);
            Assert.AreEqual(50, player.Exp);
        }

        [TestMethod]
        public void GrantExp_ReachesThreshold_IncrementsLevel()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(101);

            Assert.AreEqual(2, player.Level);
            Assert.AreEqual(1, player.Exp);        // 101 - 100 = 1 carried over
        }

        [TestMethod]
        public void GrantExp_OnLevelUp_RaisesPlayerLeveledUpEvent()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(101);

            var evt = player.DomainEvents.OfType<PlayerLeveledUpEvent>().SingleOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(player.Id, evt.PlayerId);
            Assert.AreEqual(2, evt.NewLevel);
        }

        [TestMethod]
        public void GrantExp_NoLevelUp_NoDomainEvents()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(50);

            Assert.AreEqual(0, player.DomainEvents.Count);
        }

        [TestMethod]
        public void GrantExp_LevelUp_GrantsSixStatPoints()
        {
            var player = MakePlayer(level: 1, exp: 0);
            var before = player.StatPoints.StatPointsGained;

            player.GrantExp(101);

            Assert.AreEqual(before + 6, player.StatPoints.StatPointsGained);
        }

        // ── GrantExp — multi-level-up ────────────────────────────────────────

        [TestMethod]
        public void GrantExp_EnoughForTwoLevels_LevelsTwice()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301); // threshold 100 (lvl1) + 200 (lvl2) = 300 to reach lvl3

            Assert.AreEqual(3, player.Level);
            Assert.AreEqual(1, player.Exp);
        }

        [TestMethod]
        public void GrantExp_MultiLevelUp_RaisesOneEventPerLevel()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301);

            var events = player.DomainEvents.OfType<PlayerLeveledUpEvent>().ToList();
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(2, events[0].NewLevel);
            Assert.AreEqual(3, events[1].NewLevel);
        }

        [TestMethod]
        public void GrantExp_MultiLevelUp_AccumulatesStatPoints()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301);

            Assert.AreEqual(12, player.StatPoints.StatPointsGained); // 6 per level * 2
        }

        [TestMethod]
        public void GrantExp_ExactlyAtThreshold_DoesNotLevelUp()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(100); // threshold is > 100, not >=

            Assert.AreEqual(1, player.Level);
            Assert.AreEqual(100, player.Exp);
        }

        // ── AddInventoryItem ─────────────────────────────────────────────────

        [TestMethod]
        public void AddInventoryItem_AddsItemToInventory()
        {
            var player = MakePlayer();
            var item = MakeItem(id: 10, name: "Sword");

            player.AddInventoryItem(item, inventoryItemId: 99);

            Assert.AreEqual(1, player.Inventory.InventorySlots.Count);
            Assert.AreEqual(item, player.Inventory.InventorySlots[0].Item);
        }

        [TestMethod]
        public void AddInventoryItem_RaisesItemAcquiredEvent()
        {
            var player = MakePlayer();
            var item = MakeItem(id: 10, name: "Sword");

            player.AddInventoryItem(item, inventoryItemId: 99);

            var evt = player.DomainEvents.OfType<ItemAcquiredEvent>().SingleOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(player.Id, evt.PlayerId);
            Assert.AreEqual(99, evt.InventoryItemId);
            Assert.AreEqual(item, evt.Item);
        }

        // ── RecordEnemyDefeat ────────────────────────────────────────────────

        [TestMethod]
        public void RecordEnemyDefeat_RaisesEnemyDefeatedEvent()
        {
            var player = MakePlayer();
            var drops = new List<Item> { MakeItem(1, "Drop") };

            player.RecordEnemyDefeat(enemyId: 5, expReward: 200, drops: drops);

            var evt = player.DomainEvents.OfType<EnemyDefeatedEvent>().SingleOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(player.Id, evt.PlayerId);
            Assert.AreEqual(5, evt.EnemyId);
            Assert.AreEqual(200, evt.ExpReward);
            Assert.AreEqual(1, evt.DroppedItems.Count);
        }

        // ── ClearEvents ──────────────────────────────────────────────────────

        [TestMethod]
        public void ClearEvents_RemovesAllCollectedEvents()
        {
            var player = MakePlayer(level: 1, exp: 0);
            player.GrantExp(101);               // produces one event
            Assert.IsTrue(player.DomainEvents.Count > 0);

            player.ClearEvents();

            Assert.AreEqual(0, player.DomainEvents.Count);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Player MakePlayer(int level = 1, int exp = 0) => new()
        {
            Id = 1,
            Name = "Test",
            Level = level,
            Exp = exp,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([]) { StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
        };

        private static Item MakeItem(int id, string name) => new()
        {
            Id = id,
            Name = name,
            Description = string.Empty,
            Category = EItemCategory.Accessory,
            Attributes = [],
            ModSlots = [],
            Tags = [],
        };
    }
}
