using Game.Application.Services;
using Game.Application.Tests.Fakes;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Events;
using Game.Core.Players;
using Game.Core.Players.Inventories;

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

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Creates a wired-up BattleService together with its fakes.</summary>
        private static (BattleService service, FakePlayerRepository repo, FakeDispatcher dispatcher)
            MakeService(Enemy? domainEnemy = null)
        {
            var repo = new FakePlayerRepository();
            var world = new FakeWorldRepository(domainEnemy);
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

        private static Enemy MakeEnemy(int id, int level) => new()
        {
            Id = id,
            Name = "Test Enemy",
            Level = level,
            AttributeDistributions = [],   // no attributes → ExpReward = 0
            Skills = [],
            Drops = [],
        };
    }
}
