using Game.Abstractions.DataAccess;
using Game.Core.Battle;
using Game.Core.Players;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Application.Services
{
    public class BattleService(
        IPlayerRepository playerRepo,
        IWorldRepository worldRepo,
        IDomainEventDispatcher dispatcher)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IWorldRepository _worldRepo = worldRepo;
        private readonly IDomainEventDispatcher _dispatcher = dispatcher;

        public async Task<BattleStartResult> StartBattle(Player player, PlayerState state, int zoneId, int? newZoneId = null)
        {
            if (newZoneId.HasValue)
            {
                player.ChangeZone(newZoneId.Value);
                zoneId = newZoneId.Value;
                await _playerRepo.SavePlayer(player);
            }

            var zoneEntity = _worldRepo.Zones.GetZone(zoneId)
                ?? throw new InvalidOperationException($"Zone {zoneId} not found");

            var rng = new Random();
            var level = rng.Next(zoneEntity.LevelMin, zoneEntity.LevelMax + 1);

            var enemy = _worldRepo.Enemies.GetRandomDomainEnemy(zoneId, level);

            var battleRng = new Mulberry32((uint)rng.Next());
            enemy.Skills = enemy.GetRandomSkills(battleRng);

            var now = DateTime.UtcNow;
            var seed = (uint)(now.Ticks % uint.MaxValue);

            var simulator = new BattleSimulator(player, enemy);
            var victory = simulator.Simulate(out var totalMs);
            var earliestDefeat = now.AddMilliseconds(totalMs);

            state.SetActiveBattle(enemy.Id, level, seed, earliestDefeat, victory);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
            };
        }

        public async Task<DefeatResult?> TryDefeatEnemy(Player player, PlayerState state, int enemyId, int level, Mulberry32 rng)
        {
            var now = DateTime.UtcNow;
            if (!state.CanDefeatEnemy(now))
                return null;

            var enemy = _worldRepo.Enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var rewards = new DefeatRewards(player, enemy, rng);

            // GrantExp raises PlayerLeveledUpEvent internally if a level-up occurs.
            player.GrantExp(rewards.ExpReward);

            var droppedItems = new List<DroppedItemInfo>();
            foreach (var droppedItem in rewards.Drops)
            {
                var slotNumber = player.Inventory.GetNextFreeSlotNumber();
                var inventoryItemId = await _playerRepo.AddInventoryItem(
                    player.Id, droppedItem.Id, slotNumber);

                // AddInventoryItem updates the in-memory inventory and raises ItemAcquiredEvent.
                player.AddInventoryItem(droppedItem, inventoryItemId);

                droppedItems.Add(new DroppedItemInfo
                {
                    InventoryItemId = inventoryItemId,
                    SlotNumber = slotNumber,
                    Item = droppedItem,
                });
            }

            // RecordEnemyDefeat raises EnemyDefeatedEvent.
            player.RecordEnemyDefeat(enemyId, rewards.ExpReward, droppedItems.Select(d => d.Item).ToList());

            state.SetCooldown(now.AddSeconds(5));
            state.ClearBattle();

            await _playerRepo.SavePlayer(player);

            // Dispatch all events collected during this operation, then clear.
            var events = player.DomainEvents.ToList();
            player.ClearEvents();
            await _dispatcher.DispatchAsync(events);

            return new DefeatResult
            {
                ExpReward = rewards.ExpReward,
                DroppedItems = droppedItems,
                NewLevel = player.Level,
                NewExp = player.Exp,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
            };
        }
    }

    public class BattleStartResult
    {
        public required CoreEnemy Enemy { get; set; }
        public required uint Seed { get; set; }
    }
}
