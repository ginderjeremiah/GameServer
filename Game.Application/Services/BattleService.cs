using Game.Abstractions.DataAccess;
using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Players;
using CoreEnemy = Game.Core.Enemies.Enemy;
using CoreZone = Game.Core.Zones.Zone;

namespace Game.Application.Services
{
    public class BattleService(
        IPlayerRepository playerRepo,
        IEnemies enemies,
        IZones zones,
        IPlayerProgressRepository progressRepo,
        BattleSnapshotService battleSnapshotService,
        BattleFactory battleFactory)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEnemies _enemies = enemies;
        private readonly IZones _zones = zones;
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly BattleSnapshotService _battleSnapshotService = battleSnapshotService;
        private readonly BattleFactory _battleFactory = battleFactory;

        public async Task<BattleStartResult> StartBattle(Player player, PlayerState state, int zoneId, int? newZoneId = null)
        {
            if (state.HasActiveBattle)
            {
                await AbandonBattle(player, state);
            }

            // A real zone change is gated on the target being unlocked (anti-cheat). A legitimate client
            // never navigates into a locked zone — the UI gates it — so a locked target is ignored and the
            // battle simply proceeds in the player's current zone. Same-zone re-requests skip the check (and
            // the redundant save) entirely.
            if (newZoneId.HasValue && newZoneId.Value != player.CurrentZoneId)
            {
                var targetZone = _zones.GetDomainZone(newZoneId.Value);
                if (await IsZoneUnlocked(player.Id, targetZone))
                {
                    player.ChangeZone(newZoneId.Value);
                    zoneId = newZoneId.Value;
                    await _playerRepo.SavePlayer(player);
                }
            }

            var zone = _zones.GetDomainZone(zoneId);

            var now = DateTime.UtcNow;
            var seed = CreateBattleSeed(now);

            var enemy = _battleFactory.CreateBattleEnemy(
                zone,
                level => _enemies.GetRandomDomainEnemy(zone.Id, level));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = _battleSnapshotService.CreateSnapshot(player);

            state.SetActiveBattle(enemy.Id, enemy.Level, enemySkillIds, seed, now, snapshot, zone.Id, isBossBattle: false);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
            };
        }

        /// <summary>
        /// Starts a deterministic battle against the zone's dedicated boss (the "Challenge Boss" action),
        /// separate from the random idle spawn. The boss is fought at the zone's fixed level with its full
        /// authored skill loadout. Returns <c>null</c> when the zone has no dedicated boss authored. Unlike
        /// <see cref="StartBattle"/> there is no cooldown gate — the boss challenge is always available — and
        /// challenging does not change the player's current zone.
        /// </summary>
        public async Task<BattleStartResult?> StartBossBattle(Player player, PlayerState state, int zoneId)
        {
            var zone = _zones.GetDomainZone(zoneId);

            // Validate the challenge before touching the active battle: abandoning is not a cheap no-op
            // (it force-resolves and persists the current battle), so a challenge against a bossless or
            // locked zone must be a true no-op rather than silently ending the player's in-progress fight.
            if (zone.BossEnemyId is not int bossEnemyId)
            {
                return null;
            }

            // Anti-cheat: a locked zone's boss cannot be challenged. A legitimate client can never be in a
            // locked zone to begin with, so this only blocks tampered requests.
            if (!await IsZoneUnlocked(player.Id, zone))
            {
                return null;
            }

            if (state.HasActiveBattle)
            {
                await AbandonBattle(player, state);
            }

            var now = DateTime.UtcNow;
            var seed = CreateBattleSeed(now);

            var enemy = _battleFactory.CreateBossEnemy(
                zone,
                level => _enemies.GetDomainEnemy(bossEnemyId, level)
                    ?? throw new InvalidOperationException(
                        $"Zone {zone.Id} references boss enemy {bossEnemyId}, which does not exist."));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = _battleSnapshotService.CreateSnapshot(player);

            state.SetActiveBattle(enemy.Id, enemy.Level, enemySkillIds, seed, now, snapshot, zone.Id, isBossBattle: true);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
            };
        }

        public async Task<DefeatResult?> EndBattleVictory(Player player, PlayerState state, DateTime claimedTimestamp)
        {
            if (state.ActiveEnemyId is not int enemyId || state.Snapshot is null)
            {
                return null;
            }

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

            var rewards = RecordVictory(player, enemy, result, state);

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
            if (state.ActiveEnemyId is not int enemyId || state.Snapshot is null)
            {
                return false;
            }

            var level = state.ActiveEnemyLevel ?? 1;
            var enemySkillIds = state.ActiveEnemySkillIds ?? [];

            var enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            var result = SimulateBattle(enemy, enemySkillIds, state.Snapshot);

            if (result.Victory)
            {
                return false;
            }

            player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId);

            state.SetCooldown(DateTime.UtcNow.AddSeconds(5));
            state.ClearBattle();

            await _playerRepo.SavePlayer(player);

            return true;
        }

        private async Task AbandonBattle(Player player, PlayerState state)
        {
            if (state.ActiveEnemyId is not int enemyId || state.Snapshot is null)
            {
                state.ClearBattle();
                return;
            }

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

            if (result.Victory)
            {
                // The enemy died within the elapsed wall-clock time, so this abandon resolved as a real
                // victory. Pay it out exactly like EndBattleVictory (exp + win/clear/challenge credit)
                // rather than booking the win while silently withholding the earned exp (#206).
                RecordVictory(player, enemy, result, state);
            }
            else
            {
                player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId);
            }

            state.ClearBattle();

            await _playerRepo.SavePlayer(player);
        }

        // Books a victory: grants the earned exp and records the win (kills, zone clears, and the
        // challenge rewards driven off BattleCompletedEvent). Shared by the explicit victory path and
        // the won-abandon path so the two cannot drift — a battle that resolved as a win always pays
        // out the same way, regardless of how the battle was ended.
        private static DefeatRewards RecordVictory(Player player, CoreEnemy enemy, BattleResult result, PlayerState state)
        {
            var rewards = new DefeatRewards(player, enemy);

            player.GrantExp(rewards.ExpReward);
            player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId);

            return rewards;
        }

        // Whether a zone is unlocked for the player. An ungated zone is always open and pays no read cost;
        // a gated zone costs one indexed completion lookup, incurred only on a real zone transition or a
        // boss challenge (not per idle tick). The unlock rule itself lives on the domain Zone.
        private async Task<bool> IsZoneUnlocked(int playerId, CoreZone zone)
        {
            if (zone.UnlockChallengeId is null)
            {
                return true;
            }

            var completedChallengeIds = await _progressRepo.GetCompletedChallengeIds(playerId);
            return zone.IsUnlocked(completedChallengeIds);
        }

        // Derives the simulation RNG seed from the battle-start timestamp. Shared by both start paths so
        // seed generation stays in one place if it ever changes.
        private static uint CreateBattleSeed(DateTime now) => (uint)(now.Ticks % uint.MaxValue);

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
