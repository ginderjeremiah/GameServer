using Game.Abstractions.DataAccess;
using Game.Core.Battle;
using Game.Core.Players;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Application.Services
{
    public class BattleService(
        IPlayerRepository playerRepo,
        IEnemies enemies,
        IZones zones,
        IDomainEventDispatcher dispatcher)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEnemies _enemies = enemies;
        private readonly IZones _zones = zones;
        private readonly IDomainEventDispatcher _dispatcher = dispatcher;

        public async Task<BattleStartResult> StartBattle(Player player, PlayerState state, int zoneId, int? newZoneId = null)
        {
            if (newZoneId.HasValue)
            {
                player.ChangeZone(newZoneId.Value);
                zoneId = newZoneId.Value;
                await _playerRepo.SavePlayer(player);
            }

            var zoneEntity = _zones.GetZone(zoneId)
                ?? throw new InvalidOperationException($"Zone {zoneId} not found");

            var rng = new Random();
            var level = rng.Next(zoneEntity.LevelMin, zoneEntity.LevelMax + 1);

            var enemy = _enemies.GetRandomDomainEnemy(zoneId, level);

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

            var enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var rewards = new DefeatRewards(player, enemy);

            player.GrantExp(rewards.ExpReward);
            player.RecordEnemyDefeat(enemyId, rewards.ExpReward);

            state.SetCooldown(now.AddSeconds(5));
            state.ClearBattle();

            await _playerRepo.SavePlayer(player);

            var events = player.DomainEvents.ToList();
            player.ClearEvents();
            await _dispatcher.DispatchAsync(events);

            return new DefeatResult
            {
                ExpReward = rewards.ExpReward,
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
