using Game.Core.Battle.Events;
using Game.Core.Players;
using Game.Core.Players.Events;
using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class PlayerTests
    {
        // ── GrantExp ─────────────────────────────────────────────────────────

        [Fact]
        public void GrantExp_BelowLevelThreshold_DoesNotLevelUp()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(50); // threshold is 1 * 100 = 100

            Assert.Equal(1, player.Level);
            Assert.Equal(50, player.Exp);
        }

        [Fact]
        public void GrantExp_ReachesThreshold_IncrementsLevel()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(101);

            Assert.Equal(2, player.Level);
            Assert.Equal(1, player.Exp);        // 101 - 100 = 1 carried over
        }

        [Fact]
        public void GrantExp_OnLevelUp_RaisesPlayerLeveledUpEvent()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(101);

            var evt = player.DomainEvents.OfType<PlayerLeveledUpEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(2, evt.NewLevel);
        }

        [Fact]
        public void GrantExp_NoLevelUp_OnlyCoreUpdatedEvent()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(50);

            Assert.Single(player.DomainEvents);
            Assert.IsType<PlayerCoreUpdatedEvent>(player.DomainEvents[0]);
        }

        [Fact]
        public void GrantExp_LevelUp_GrantsSixStatPoints()
        {
            var player = MakePlayer(level: 1, exp: 0);
            var before = player.StatPoints.StatPointsGained;

            player.GrantExp(101);

            Assert.Equal(before + 6, player.StatPoints.StatPointsGained);
        }

        // ── GrantExp — multi-level-up ────────────────────────────────────────

        [Fact]
        public void GrantExp_EnoughForTwoLevels_LevelsTwice()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301); // threshold 100 (lvl1) + 200 (lvl2) = 300 to reach lvl3

            Assert.Equal(3, player.Level);
            Assert.Equal(1, player.Exp);
        }

        [Fact]
        public void GrantExp_MultiLevelUp_RaisesOneEventPerLevel()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301);

            var events = player.DomainEvents.OfType<PlayerLeveledUpEvent>().ToList();
            Assert.Equal(2, events.Count);
            Assert.Equal(2, events[0].NewLevel);
            Assert.Equal(3, events[1].NewLevel);
        }

        [Fact]
        public void GrantExp_MultiLevelUp_AccumulatesStatPoints()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301);

            Assert.Equal(12, player.StatPoints.StatPointsGained); // 6 per level * 2
        }

        [Fact]
        public void GrantExp_ExactlyAtThreshold_DoesNotLevelUp()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(100); // threshold is > 100, not >=

            Assert.Equal(1, player.Level);
            Assert.Equal(100, player.Exp);
        }

        // ── UnlockItem ──────────────────────────────────────────────────────

        [Fact]
        public void UnlockItem_AddsItemToInventory()
        {
            var player = MakePlayer();

            player.UnlockItem(10);

            Assert.Single(player.Inventory.UnlockedItems);
            Assert.Equal(10, player.Inventory.UnlockedItems[0].ItemId);
        }

        [Fact]
        public void UnlockItem_RaisesItemUnlockedEvent()
        {
            var player = MakePlayer();

            player.UnlockItem(10);

            var evt = player.DomainEvents.OfType<ItemUnlockedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(10, evt.ItemId);
        }

        // ── UnlockMod ───────────────────────────────────────────────────────

        [Fact]
        public void UnlockMod_AddsModToInventory()
        {
            var player = MakePlayer();

            player.UnlockMod(5);

            Assert.Contains(5, player.Inventory.UnlockedMods);
        }

        [Fact]
        public void UnlockMod_RaisesModUnlockedEvent()
        {
            var player = MakePlayer();

            player.UnlockMod(5);

            var evt = player.DomainEvents.OfType<ModUnlockedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(5, evt.ItemModId);
        }

        // ── RecordEnemyDefeat ────────────────────────────────────────────────

        [Fact]
        public void RecordEnemyDefeat_RaisesEnemyDefeatedEvent()
        {
            var player = MakePlayer();

            player.RecordEnemyDefeat(enemyId: 5, expReward: 200);

            var evt = player.DomainEvents.OfType<EnemyDefeatedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player, evt.Player);
            Assert.Equal(5, evt.EnemyId);
            Assert.Equal(200, evt.ExpReward);
        }

        // ── ClearEvents ──────────────────────────────────────────────────────

        [Fact]
        public void ClearEvents_RemovesAllCollectedEvents()
        {
            var player = MakePlayer(level: 1, exp: 0);
            player.GrantExp(101);               // produces one event
            Assert.True(player.DomainEvents.Count > 0);

            player.ClearEvents();

            Assert.Empty(player.DomainEvents);
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
