using Game.Application.Services;
using Game.Application.Tests.Fakes;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Events;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using CoreEnemy = Game.Core.Enemies.Enemy;
using EntityZone = Game.Abstractions.Entities.Zone;

namespace Game.Application.Tests.Services
{
    [TestClass]
    public class BattleServiceTests
    {
        // ── TryDefeatEnemy ───────────────────────────────────────────────────

        [TestMethod]
        public async Task TryDefeatEnemy_WhenCannotDefeat_ReturnsNull()
        {
            var (service, _, _) = MakeService();
            var player = MakePlayer();

            // State with no active battle — CanDefeatEnemy returns false.
            var state = new PlayerState { Victory = true };
            // ActiveEnemyId is null → CanDefeatEnemy == false

            var result = await service.TryDefeatEnemy(player, state, enemyId: 1, level: 1, rng: new Mulberry32(42u));

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task TryDefeatEnemy_WhenVictoryNotSet_ReturnsNull()
        {
            var (service, _, _) = MakeService();
            var player = MakePlayer();

            var state = new PlayerState();
            // victory: false → CanDefeatEnemy == false even if time has elapsed.
            state.SetActiveBattle(1, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: false);

            var result = await service.TryDefeatEnemy(player, state, enemyId: 1, level: 1, rng: new Mulberry32(42u));

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task TryDefeatEnemy_SuccessfulDefeat_ReturnsDefeatResult()
        {
            var enemy = MakeEnemy(id: 1, level: 1);
            var (service, repo, dispatcher) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            var result = await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task TryDefeatEnemy_SuccessfulDefeat_PersistsPlayer()
        {
            var enemy = MakeEnemy(id: 1, level: 1);
            var (service, repo, _) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            // SavePlayer must have been called exactly once to persist the updated player.
            Assert.AreEqual(1, repo.SavePlayerCallCount);
        }

        [TestMethod]
        public async Task TryDefeatEnemy_SuccessfulDefeat_ClearsBattleState()
        {
            var enemy = MakeEnemy(id: 1, level: 1);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            // After defeat the battle should be cleared.
            Assert.IsNull(state.ActiveEnemyId);
            Assert.IsFalse(state.Victory);
        }

        [TestMethod]
        public async Task TryDefeatEnemy_SuccessfulDefeat_DispatchesEnemyDefeatedEvent()
        {
            var enemy = MakeEnemy(id: 1, level: 1);
            var (service, _, dispatcher) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            var defeatedEvent = dispatcher.DispatchedEvents.OfType<EnemyDefeatedEvent>().SingleOrDefault();
            Assert.IsNotNull(defeatedEvent);
            Assert.AreEqual(player.Id, defeatedEvent.PlayerId);
            Assert.AreEqual(enemy.Id, defeatedEvent.EnemyId);
        }

        [TestMethod]
        public async Task TryDefeatEnemy_SuccessfulDefeat_PlayerEventsAreClearedAfterDispatch()
        {
            var enemy = MakeEnemy(id: 1, level: 1);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            // Domain events must be cleared after dispatch so they are not replayed.
            Assert.AreEqual(0, player.DomainEvents.Count);
        }

        // ── StartBattle ──────────────────────────────────────────────────────

        [TestMethod]
        public async Task StartBattle_ValidZone_ReturnsBattleStartResult()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 5);
            var zone = MakeZone(id: 0, levelMin: 1, levelMax: 10);
            var (service, _, _) = MakeService(domainEnemy: enemy, zone: zone);
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            var result = await service.StartBattle(player, state, zoneId: 0);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Enemy);
            Assert.IsTrue(result.Seed > 0 || result.Seed == 0);
        }

        [TestMethod]
        public async Task StartBattle_SetsActiveBattleState()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 5);
            var zone = MakeZone(id: 0, levelMin: 1, levelMax: 10);
            var (service, _, _) = MakeService(domainEnemy: enemy, zone: zone);
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            await service.StartBattle(player, state, zoneId: 0);

            Assert.IsNotNull(state.ActiveEnemyId);
            Assert.IsNotNull(state.ActiveEnemyLevel);
            Assert.IsNotNull(state.BattleSeed);
        }

        [TestMethod]
        public async Task StartBattle_WithNewZoneId_ChangesPlayerZone()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 5);
            var zone = MakeZone(id: 2, levelMin: 1, levelMax: 10);
            var (service, repo, _) = MakeService(domainEnemy: enemy, zone: zone);
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            await service.StartBattle(player, state, zoneId: 0, newZoneId: 2);

            Assert.AreEqual(2, player.CurrentZoneId);
            Assert.AreEqual(1, repo.SavePlayerCallCount);
        }

        [TestMethod]
        public async Task StartBattle_InvalidZone_Throws()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 5);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.StartBattle(player, state, zoneId: 999));
        }

        // ── TryDefeatEnemy — drops ──────────────────────────────────────────

        [TestMethod]
        public async Task TryDefeatEnemy_WithDrops_AddsItemsToInventory()
        {
            var item1 = MakeItem(1, "Sword");
            var item2 = MakeItem(2, "Shield");
            var enemy = MakeEnemyWithDrops(id: 1, level: 1, drops: [item1, item2]);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            var result = await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.DroppedItems.Count);
            Assert.AreEqual(0, result.DroppedItems[0].SlotNumber);
            Assert.AreEqual(1, result.DroppedItems[1].SlotNumber);
        }

        [TestMethod]
        public async Task TryDefeatEnemy_WithDrops_RaisesItemAcquiredEvents()
        {
            var item1 = MakeItem(1, "Sword");
            var enemy = MakeEnemyWithDrops(id: 1, level: 1, drops: [item1]);
            var (service, _, dispatcher) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            var acquiredEvents = dispatcher.DispatchedEvents.OfType<ItemAcquiredEvent>().ToList();
            Assert.AreEqual(1, acquiredEvents.Count);
        }

        // ── TryDefeatEnemy — progression data ───────────────────────────────

        [TestMethod]
        public async Task TryDefeatEnemy_ReturnsPlayerProgressionData()
        {
            var enemy = MakeEnemy(id: 1, level: 1);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakePlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddSeconds(-1), victory: true);

            var result = await service.TryDefeatEnemy(player, state, enemyId: enemy.Id, level: 1, rng: new Mulberry32(42u));

            Assert.IsNotNull(result);
            Assert.AreEqual(player.Level, result.NewLevel);
            Assert.AreEqual(player.Exp, result.NewExp);
            Assert.AreEqual(player.StatPoints.StatPointsGained, result.StatPointsGained);
            Assert.AreEqual(player.StatPoints.StatPointsUsed, result.StatPointsUsed);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Creates a wired-up BattleService together with its fakes.</summary>
        private static (BattleService service, FakePlayerRepository repo, FakeDispatcher dispatcher)
            MakeService(CoreEnemy? domainEnemy = null, EntityZone? zone = null)
        {
            var repo = new FakePlayerRepository();
            var world = new FakeWorldRepository(domainEnemy, zone);
            var dispatcher = new FakeDispatcher();
            var service = new BattleService(repo, world, dispatcher);
            return (service, repo, dispatcher);
        }

        private static Player MakePlayer(int level = 1) => new()
        {
            Id = 1,
            Name = "Hero",
            Level = level,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([])
                { StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
        };

        private static Player MakeBattleReadyPlayer() => new()
        {
            Id = 1,
            Name = "Hero",
            Level = 5,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([
                new StatAllocation { Attribute = EAttribute.Strength, Amount = 50 },
                new StatAllocation { Attribute = EAttribute.Endurance, Amount = 50 },
            ]) { StatPointsGained = 100, StatPointsUsed = 100 },
            Inventory = new Inventory(),
            SelectedSkills =
            [
                new Skill
                {
                    Id = 0, Name = "Attack", Description = "",
                    CooldownMs = 1000, BaseDamage = 10,
                    DamageMultipliers = [new AttributeModifier
                    {
                        Attribute = EAttribute.Strength, Amount = 1.0,
                        Type = EModifierType.Multiplicative,
                        Source = EAttributeModifierSource.Derived,
                    }],
                },
            ],
            Skills = [],
        };

        private static CoreEnemy MakeEnemy(int id, int level) => new()
        {
            Id = id,
            Name = "Test Enemy",
            Level = level,
            AttributeDistributions = [],
            Skills = [],
            Drops = [],
        };

        private static CoreEnemy MakeBattleReadyEnemy(int id, int level) => new()
        {
            Id = id,
            Name = "Battle Enemy",
            Level = level,
            AttributeDistributions =
            [
                new AttributeDistribution
                {
                    AttributeId = EAttribute.Strength, BaseAmount = 5, AmountPerLevel = 1,
                },
                new AttributeDistribution
                {
                    AttributeId = EAttribute.Endurance, BaseAmount = 5, AmountPerLevel = 1,
                },
            ],
            Skills =
            [
                new Skill
                {
                    Id = 0, Name = "Scratch", Description = "",
                    CooldownMs = 1500, BaseDamage = 5, DamageMultipliers = [],
                },
            ],
            Drops = [],
        };

        private static CoreEnemy MakeEnemyWithDrops(int id, int level, List<Item> drops) => new()
        {
            Id = id,
            Name = "Drop Enemy",
            Level = level,
            AttributeDistributions = [],
            Skills = [],
            Drops = drops.Select(item => new EnemyDrop
            {
                Item = item,
                DropRate = decimal.MaxValue,
            }).ToList(),
        };

        private static Item MakeItem(int id, string name) => new()
        {
            Id = id, Name = name, Description = "",
            Category = EItemCategory.Weapon, Attributes = [], ModSlots = [], Tags = [],
        };

        private static EntityZone MakeZone(int id, int levelMin, int levelMax) => new()
        {
            Id = id, Name = "Test Zone", Description = "",
            Order = 0, LevelMin = levelMin, LevelMax = levelMax,
            ZoneDrops = [], ZoneEnemies = [],
        };
    }
}
