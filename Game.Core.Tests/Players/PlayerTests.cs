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
        public void GrantExp_NoLevelUp_OnlyCoreUpdatedEvent()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(50);

            Assert.AreEqual(1, player.DomainEvents.Count);
            Assert.IsInstanceOfType<PlayerCoreUpdatedEvent>(player.DomainEvents[0]);
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

        // ── UnlockItem ──────────────────────────────────────────────────────

        [TestMethod]
        public void UnlockItem_AddsItemToInventory()
        {
            var player = MakePlayer();

            player.UnlockItem(10);

            Assert.AreEqual(1, player.Inventory.UnlockedItems.Count);
            Assert.AreEqual(10, player.Inventory.UnlockedItems[0].ItemId);
        }

        [TestMethod]
        public void UnlockItem_RaisesItemUnlockedEvent()
        {
            var player = MakePlayer();

            player.UnlockItem(10);

            var evt = player.DomainEvents.OfType<ItemUnlockedEvent>().SingleOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(player.Id, evt.PlayerId);
            Assert.AreEqual(10, evt.ItemId);
        }

        // ── UnlockMod ───────────────────────────────────────────────────────

        [TestMethod]
        public void UnlockMod_AddsModToInventory()
        {
            var player = MakePlayer();

            player.UnlockMod(5);

            Assert.IsTrue(player.Inventory.UnlockedMods.Contains(5));
        }

        [TestMethod]
        public void UnlockMod_RaisesModUnlockedEvent()
        {
            var player = MakePlayer();

            player.UnlockMod(5);

            var evt = player.DomainEvents.OfType<ModUnlockedEvent>().SingleOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(player.Id, evt.PlayerId);
            Assert.AreEqual(5, evt.ItemModId);
        }

        // ── RecordEnemyDefeat ────────────────────────────────────────────────

        [TestMethod]
        public void RecordEnemyDefeat_RaisesEnemyDefeatedEvent()
        {
            var player = MakePlayer();

            player.RecordEnemyDefeat(enemyId: 5, expReward: 200);

            var evt = player.DomainEvents.OfType<EnemyDefeatedEvent>().SingleOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(player.Id, evt.PlayerId);
            Assert.AreEqual(5, evt.EnemyId);
            Assert.AreEqual(200, evt.ExpReward);
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
            LogPreferences = [],
        };
    }
}
