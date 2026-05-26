using Game.Abstractions.DataAccess;
using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Players;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Application.Services
{
    public class BattleService(
        IPlayerRepository playerRepo,
        IEnemies enemies,
        IZones zones,
        IDomainEventDispatcher dispatcher,
        BattlerFactory battlerFactory)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEnemies _enemies = enemies;
        private readonly IZones _zones = zones;
        private readonly IDomainEventDispatcher _dispatcher = dispatcher;
        private readonly BattlerFactory _battlerFactory = battlerFactory;

        public async Task<BattleStartResult> StartBattle(Player player, PlayerState state, int zoneId, int? newZoneId = null)
        {
            if (state.HasActiveBattle)
            {
                await AbandonBattle(player, state);
            }

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
            var snapshot = _battlerFactory.CreateSnapshot(player);

            state.SetActiveBattle(enemy.Id, level, seed, now, snapshot);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
            };
        }

        public async Task<DefeatResult?> EndBattleVictory(Player player, PlayerState state, DateTime claimedTimestamp)
        {
            if (!state.HasActiveBattle || state.Snapshot is null)
            {
                return null;
            }

            var enemyId = state.ActiveEnemyId!.Value;
            var level = state.ActiveEnemyLevel ?? 1;
            var seed = state.BattleSeed ?? 0;

            var result = SimulateBattle(enemyId, level, seed, state.Snapshot);

            if (!result.Victory)
            {
                return null;
            }

            var earliestDefeat = state.BattleStartTime.AddMilliseconds(result.TotalMs);
            var now = DateTime.UtcNow;

            if (claimedTimestamp < earliestDefeat || claimedTimestamp > now)
            {
                return null;
            }

            var enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var rewards = new DefeatRewards(player, enemy);

            player.GrantExp(rewards.ExpReward);
            player.RecordEnemyDefeat(enemyId, rewards.ExpReward);
            player.RecordBattleCompleted(enemyId, result);

            state.SetCooldown(claimedTimestamp.AddSeconds(5));
            state.ClearBattle();

            await _playerRepo.SavePlayer(player);
            await DispatchEvents(player);

            return new DefeatResult
            {
                ExpReward = rewards.ExpReward,
                NewLevel = player.Level,
                NewExp = player.Exp,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
            };
        }

        public async Task<bool> EndBattleLoss(Player player, PlayerState state)
        {
            if (!state.HasActiveBattle || state.Snapshot is null)
            {
                return false;
            }

            var enemyId = state.ActiveEnemyId!.Value;
            var level = state.ActiveEnemyLevel ?? 1;
            var seed = state.BattleSeed ?? 0;

            var result = SimulateBattle(enemyId, level, seed, state.Snapshot);

            if (result.Victory)
            {
                return false;
            }

            player.RecordBattleCompleted(enemyId, result);

            state.SetCooldown(DateTime.UtcNow.AddSeconds(5));
            state.ClearBattle();

            await _playerRepo.SavePlayer(player);
            await DispatchEvents(player);

            return true;
        }

        private async Task AbandonBattle(Player player, PlayerState state)
        {
            if (state.Snapshot is null)
            {
                state.ClearBattle();
                return;
            }

            var enemyId = state.ActiveEnemyId!.Value;
            var level = state.ActiveEnemyLevel ?? 1;
            var seed = state.BattleSeed ?? 0;

            var elapsedMs = (int)(DateTime.UtcNow - state.BattleStartTime).TotalMilliseconds;
            if (elapsedMs <= 0)
            {
                state.ClearBattle();
                return;
            }

            var result = SimulateBattle(enemyId, level, seed, state.Snapshot, elapsedMs);

            player.RecordBattleCompleted(enemyId, result);

            state.ClearBattle();

            await _playerRepo.SavePlayer(player);
            await DispatchEvents(player);
        }

        private BattleResult SimulateBattle(int enemyId, int level, uint seed, BattleSnapshot snapshot, int? maxMs = null)
        {
            var enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var battleRng = new Mulberry32(seed);
            enemy.Skills = enemy.GetRandomSkills(battleRng);

            var playerBattler = _battlerFactory.CreateFromSnapshot(snapshot);
            var enemyBattler = new Battler(
                new AttributeCollection(enemy.GetAttributeModifiers()),
                enemy.Skills,
                enemy.Level);

            var simulator = new BattleSimulator(playerBattler, enemyBattler);
            return simulator.Simulate(maxMs);
        }

        private async Task DispatchEvents(Player player)
        {
            var events = player.DomainEvents.ToList();
            player.ClearEvents();
            await _dispatcher.DispatchAsync(events);
        }
    }

    public class BattleStartResult
    {
        public required CoreEnemy Enemy { get; set; }
        public required uint Seed { get; set; }
    }
}
