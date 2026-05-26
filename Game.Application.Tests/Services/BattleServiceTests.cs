using Game.Application.Services;
using Game.Application.Tests.Fakes;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Events;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using CoreEnemy = Game.Core.Enemies.Enemy;
using EntitySkill = Game.Abstractions.Entities.Skill;
using EntityZone = Game.Abstractions.Entities.Zone;

namespace Game.Application.Tests.Services
{
    [TestClass]
    public class BattleServiceTests
    {
        // ── EndBattleVictory ─────────────────────────────────────────────────

        [TestMethod]
        public async Task EndBattleVictory_NoActiveBattle_ReturnsNull()
        {
            var (service, _, _) = MakeService();
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            var result = await service.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task EndBattleVictory_SimulationShowsLoss_ReturnsNull()
        {
            var enemy = MakeStrongEnemy(id: 1, level: 1);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakeWeakPlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddMinutes(-10), MakeSnapshot(player));

            var result = await service.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task EndBattleVictory_TimestampTooEarly_ReturnsNull()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 1);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakeBattleReadyPlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow, MakeSnapshot(player));

            // Timestamp is "now" but battle just started — too early.
            var result = await service.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task EndBattleVictory_ValidTimestamp_ReturnsDefeatResult()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 1);
            var (service, repo, dispatcher) = MakeService(domainEnemy: enemy);
            var player = MakeBattleReadyPlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddMinutes(-10), MakeSnapshot(player));

            var result = await service.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ExpReward >= 0);
        }

        [TestMethod]
        public async Task EndBattleVictory_Success_PersistsPlayerAndDispatches()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 1);
            var (service, repo, dispatcher) = MakeService(domainEnemy: enemy);
            var player = MakeBattleReadyPlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddMinutes(-10), MakeSnapshot(player));

            await service.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.AreEqual(1, repo.SavePlayerCallCount);

            var defeatedEvent = dispatcher.DispatchedEvents.OfType<EnemyDefeatedEvent>().SingleOrDefault();
            Assert.IsNotNull(defeatedEvent);

            var battleEvent = dispatcher.DispatchedEvents.OfType<BattleCompletedEvent>().SingleOrDefault();
            Assert.IsNotNull(battleEvent);
            Assert.IsTrue(battleEvent.Victory);
        }

        [TestMethod]
        public async Task EndBattleVictory_Success_ClearsBattleAndSetsCooldown()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 1);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakeBattleReadyPlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddMinutes(-10), MakeSnapshot(player));

            await service.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.IsFalse(state.HasActiveBattle);
            Assert.IsTrue(state.IsOnCooldown(DateTime.UtcNow));
        }

        // ── EndBattleLoss ────────────────────────────────────────────────────

        [TestMethod]
        public async Task EndBattleLoss_NoActiveBattle_ReturnsFalse()
        {
            var (service, _, _) = MakeService();
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            var result = await service.EndBattleLoss(player, state);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task EndBattleLoss_SimulationShowsVictory_ReturnsFalse()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 1);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakeBattleReadyPlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddMinutes(-10), MakeSnapshot(player));

            var result = await service.EndBattleLoss(player, state);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task EndBattleLoss_ValidLoss_ReturnsTrue()
        {
            var enemy = MakeStrongEnemy(id: 1, level: 1);
            var (service, repo, dispatcher) = MakeService(domainEnemy: enemy);
            var player = MakeWeakPlayer();

            var state = new PlayerState();
            state.SetActiveBattle(enemy.Id, 1, 0u, DateTime.UtcNow.AddMinutes(-10), MakeSnapshot(player));

            var result = await service.EndBattleLoss(player, state);

            Assert.IsTrue(result);
            Assert.IsFalse(state.HasActiveBattle);
            Assert.AreEqual(1, repo.SavePlayerCallCount);

            var battleEvent = dispatcher.DispatchedEvents.OfType<BattleCompletedEvent>().SingleOrDefault();
            Assert.IsNotNull(battleEvent);
            Assert.IsFalse(battleEvent.Victory);
            Assert.IsTrue(battleEvent.PlayerDied);
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
        }

        [TestMethod]
        public async Task StartBattle_SetsActiveBattleStateAndSnapshot()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 5);
            var zone = MakeZone(id: 0, levelMin: 1, levelMax: 10);
            var (service, _, _) = MakeService(domainEnemy: enemy, zone: zone);
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            await service.StartBattle(player, state, zoneId: 0);

            Assert.IsTrue(state.HasActiveBattle);
            Assert.IsNotNull(state.ActiveEnemyId);
            Assert.IsNotNull(state.ActiveEnemyLevel);
            Assert.IsNotNull(state.BattleSeed);
            Assert.IsNotNull(state.Snapshot);
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
            Assert.IsTrue(repo.SavePlayerCallCount >= 1);
        }

        [TestMethod]
        public async Task StartBattle_InvalidZone_Throws()
        {
            var enemy = MakeBattleReadyEnemy(id: 1, level: 5);
            var (service, _, _) = MakeService(domainEnemy: enemy);
            var player = MakeBattleReadyPlayer();
            var state = new PlayerState();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.StartBattle(player, state, zoneId: 999));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (BattleService service, FakePlayerRepository repo, FakeDispatcher dispatcher)
            MakeService(CoreEnemy? domainEnemy = null, EntityZone? zone = null)
        {
            var repo = new FakePlayerRepository();
            var enemies = new FakeEnemies(domainEnemy);
            var zones = new FakeZones(zone);
            var dispatcher = new FakeDispatcher();
            var battlerFactory = new BattlerFactory(
                new FakeItems(),
                new FakeItemMods(),
                new FakeSkills(MakeEntitySkills()));
            var service = new BattleService(repo, enemies, zones, dispatcher, battlerFactory);
            return (service, repo, dispatcher);
        }

        /// <summary>
        /// Builds a <see cref="BattleSnapshot"/> that mirrors the player's current state,
        /// the same way <see cref="BattlerFactory.CreateSnapshot"/> would.
        /// </summary>
        private static BattleSnapshot MakeSnapshot(Player player) => new()
        {
            Level = player.Level,
            StatAllocations = player.StatPoints.StatAllocations
                .Select(a => new StatAllocation { Attribute = a.Attribute, Amount = a.Amount })
                .ToList(),
            EquippedItems = [],
            SkillIds = player.SelectedSkills.Select(s => s.Id).ToList(),
        };

        /// <summary>
        /// Entity-layer skills that match the domain skills used in test player/enemy helpers.
        /// Index 0 = "Attack" (battle-ready player), Index 1 = "Poke" (weak player).
        /// </summary>
        private static List<EntitySkill> MakeEntitySkills() =>
        [
            new()
            {
                Id = 0,
                Name = "Attack",
                Description = "",
                BaseDamage = 10m,
                CooldownMs = 1000,
                IconPath = "",
                SkillDamageMultipliers =
                [
                    new() { SkillId = 0, AttributeId = (int)EAttribute.Strength, Multiplier = 1.0m },
                ],
            },
            new()
            {
                Id = 1,
                Name = "Poke",
                Description = "",
                BaseDamage = 1m,
                CooldownMs = 2000,
                IconPath = "",
                SkillDamageMultipliers = [],
            },
        ];

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
            ])
            { StatPointsGained = 100, StatPointsUsed = 100 },
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
            LogPreferences = [],
        };

        private static Player MakeWeakPlayer() => new()
        {
            Id = 1,
            Name = "Weakling",
            Level = 1,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([
                new StatAllocation { Attribute = EAttribute.Strength, Amount = 1 },
                new StatAllocation { Attribute = EAttribute.Endurance, Amount = 1 },
            ])
            { StatPointsGained = 2, StatPointsUsed = 2 },
            Inventory = new Inventory(),
            SelectedSkills =
            [
                new Skill
                {
                    Id = 1, Name = "Poke", Description = "",
                    CooldownMs = 2000, BaseDamage = 1,
                    DamageMultipliers = [],
                },
            ],
            Skills = [],
            LogPreferences = [],
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
        };

        private static CoreEnemy MakeStrongEnemy(int id, int level) => new()
        {
            Id = id,
            Name = "Strong Enemy",
            Level = level,
            AttributeDistributions =
            [
                new AttributeDistribution
                {
                    AttributeId = EAttribute.Strength, BaseAmount = 200, AmountPerLevel = 0,
                },
                new AttributeDistribution
                {
                    AttributeId = EAttribute.Endurance, BaseAmount = 200, AmountPerLevel = 0,
                },
            ],
            Skills =
            [
                new Skill
                {
                    Id = 0, Name = "Crush", Description = "",
                    CooldownMs = 500, BaseDamage = 100, DamageMultipliers = [],
                },
            ],
        };

        private static EntityZone MakeZone(int id, int levelMin, int levelMax) => new()
        {
            Id = id,
            Name = "Test Zone",
            Description = "",
            Order = 0,
            LevelMin = levelMin,
            LevelMax = levelMax,
            ZoneEnemies = [],
        };
    }
}
