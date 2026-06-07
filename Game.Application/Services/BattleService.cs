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
        BattleSnapshotService battleSnapshotService,
        BattleFactory battleFactory)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEnemies _enemies = enemies;
        private readonly IZones _zones = zones;
        private readonly BattleSnapshotService _battleSnapshotService = battleSnapshotService;
        private readonly BattleFactory _battleFactory = battleFactory;

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

            var zone = _zones.GetZone(zoneId);

            var now = DateTime.UtcNow;
            var seed = (uint)(now.Ticks % uint.MaxValue);

            var enemy = _battleFactory.CreateBattleEnemy(
                zone.LevelMin,
                zone.LevelMax,
                level => _enemies.GetRandomDomainEnemy(zoneId, level));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = _battleSnapshotService.CreateSnapshot(player);

            state.SetActiveBattle(enemy.Id, enemy.Level, enemySkillIds, seed, now, snapshot);

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
            var enemySkillIds = state.ActiveEnemySkillIds ?? [];

            var enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var result = SimulateBattle(enemy, enemySkillIds, state.Snapshot);

            if (!result.Victory)
            {
                return null;
            }

            var earliestDefeat = state.BattleStartTime.AddMilliseconds(result.TotalMs);
            var now = DateTime.UtcNow;

            if (earliestDefeat - claimedTimestamp > TimeSpan.FromMilliseconds(100) || claimedTimestamp > now)
            {
                return null;
            }

            var rewards = new DefeatRewards(player, enemy);

            player.GrantExp(rewards.ExpReward);
            player.RecordBattleCompleted(enemy, result);

            state.SetCooldown(claimedTimestamp.AddSeconds(5));
            state.ClearBattle();

            await _playerRepo.SavePlayer(player);

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
            var enemySkillIds = state.ActiveEnemySkillIds ?? [];

            var enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var result = SimulateBattle(enemy, enemySkillIds, state.Snapshot);

            if (result.Victory)
            {
                return false;
            }

            player.RecordBattleCompleted(enemy, result);

            state.SetCooldown(DateTime.UtcNow.AddSeconds(5));
            state.ClearBattle();

            await _playerRepo.SavePlayer(player);

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
            var enemySkillIds = state.ActiveEnemySkillIds ?? [];

            var enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var elapsedMs = (int)(DateTime.UtcNow - state.BattleStartTime).TotalMilliseconds;
            if (elapsedMs <= 0)
            {
                state.ClearBattle();
                return;
            }

            var result = SimulateBattle(enemy, enemySkillIds, state.Snapshot, elapsedMs);

            player.RecordBattleCompleted(enemy, result);

            state.ClearBattle();

            await _playerRepo.SavePlayer(player);
        }

        private BattleResult SimulateBattle(CoreEnemy enemy, IReadOnlyList<int> enemySkillIds, BattleSnapshot snapshot, int? maxMs = null)
        {
            enemy.SetBattleSkills(enemySkillIds);

            var playerBattler = _battleSnapshotService.CreateFromSnapshot(snapshot);
            var enemyBattler = new Battler(
                new AttributeCollection(enemy.GetAttributeModifiers()),
                enemy.BattleSkills,
                enemy.Level);

            var simulator = new BattleSimulator(playerBattler, enemyBattler);
            return simulator.Simulate(maxMs);
        }
    }

    public class BattleStartResult
    {
        public required CoreEnemy Enemy { get; set; }
        public required uint Seed { get; set; }
    }
}
