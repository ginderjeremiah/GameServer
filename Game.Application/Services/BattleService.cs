using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using Game.Abstractions.DataAccess;
using Game.Core;
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
        IItems items,
        IItemMods itemMods,
        ISkills skills,
        BattleFactory battleFactory)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEnemies _enemies = enemies;
        private readonly IZones _zones = zones;
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly IItems _items = items;
        private readonly IItemMods _itemMods = itemMods;
        private readonly ISkills _skills = skills;
        private readonly BattleFactory _battleFactory = battleFactory;

        // Symmetric clock-skew tolerance for the client-claimed victory timestamp: one logical tick, because
        // the frontend's battle-start may sit up to a tick off the backend's (a battle started mid-tick counts
        // its first partial tick as a full one). Benign skew within a tick in either direction is absorbed,
        // while a claim outside this envelope on either side is rejected by the anti-cheat check in EndBattleVictory.
        private static readonly TimeSpan ClaimedTimestampSkewTolerance = TimeSpan.FromMilliseconds(GameConstants.MsPerTick);

        // Post-battle enemy cooldown, shared by the win and loss paths so the two cannot diverge. The win path
        // anchors it to the client's claimed completion time and the loss path to the server clock, but the
        // duration is identical.
        private static readonly TimeSpan PostBattleCooldown = TimeSpan.FromSeconds(5);

        public async Task<BattleStartResult> StartBattle(Player player, PlayerState state, int zoneId, int? newZoneId = null, CancellationToken cancellationToken = default)
        {
            if (state.HasActiveBattle)
            {
                await AbandonBattle(player, state, cancellationToken);
            }

            // A real zone change is gated on the target being unlocked (anti-cheat). A legitimate client
            // never navigates into a locked zone — the UI gates it — so a locked target is ignored and the
            // battle simply proceeds in the player's current zone. Same-zone re-requests skip the check (and
            // the redundant save) entirely.
            if (newZoneId.HasValue && newZoneId.Value != player.CurrentZoneId)
            {
                var targetZone = _zones.GetDomainZone(newZoneId.Value);
                if (await IsZoneUnlocked(player.Id, targetZone, cancellationToken))
                {
                    player.ChangeZone(newZoneId.Value);
                    zoneId = newZoneId.Value;
                    await _playerRepo.SavePlayer(player, cancellationToken);
                }
            }

            var zone = _zones.GetDomainZone(zoneId);

            var now = DateTime.UtcNow;
            var seed = CreateBattleSeed();

            var enemy = _battleFactory.CreateBattleEnemy(
                zone,
                level => _enemies.GetRandomDomainEnemy(zone.Id, level));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = BattleSnapshot.FromPlayer(player);

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
        public async Task<BattleStartResult?> StartBossBattle(Player player, PlayerState state, int zoneId, CancellationToken cancellationToken = default)
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
            if (!await IsZoneUnlocked(player.Id, zone, cancellationToken))
            {
                return null;
            }

            if (state.HasActiveBattle)
            {
                await AbandonBattle(player, state, cancellationToken);
            }

            var now = DateTime.UtcNow;
            var seed = CreateBattleSeed();

            var enemy = _battleFactory.CreateBossEnemy(
                zone,
                level => _enemies.GetDomainEnemy(bossEnemyId, level)
                    ?? throw new InvalidOperationException(
                        $"Zone {zone.Id} references boss enemy {bossEnemyId}, which does not exist."));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = BattleSnapshot.FromPlayer(player);

            state.SetActiveBattle(enemy.Id, enemy.Level, enemySkillIds, seed, now, snapshot, zone.Id, isBossBattle: true);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
            };
        }

        public async Task<DefeatResult?> EndBattleVictory(Player player, PlayerState state, DateTime claimedTimestamp, CancellationToken cancellationToken = default)
        {
            if (!TryResolveActiveBattle(state, out var enemy, out var result))
            {
                return null;
            }

            if (!result.Victory)
            {
                return null;
            }

            var earliestDefeat = state.BattleStartTime.AddMilliseconds(result.TotalMs);
            var now = DateTime.UtcNow;

            // Reject a claim that lands outside the symmetric skew envelope on either side: too early
            // (clock lagging) or too far in the future (clock leading) is anti-cheat, but benign skew
            // within the tolerance is accepted so a slightly-ahead client clock does not void a real win.
            if (earliestDefeat - claimedTimestamp > ClaimedTimestampSkewTolerance
                || claimedTimestamp - now > ClaimedTimestampSkewTolerance)
            {
                return null;
            }

            var rewards = RecordVictory(player, enemy, result, state);

            state.SetCooldown(claimedTimestamp + PostBattleCooldown);
            state.ClearBattle();

            await _playerRepo.SavePlayer(player, cancellationToken);

            return new DefeatResult
            {
                ExpReward = rewards.ExpReward,
                NewLevel = player.Level,
                NewExp = player.Exp,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
            };
        }

        public async Task<bool> EndBattleLoss(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            if (!TryResolveActiveBattle(state, out var enemy, out var result))
            {
                return false;
            }

            if (result.Victory)
            {
                return false;
            }

            player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId);

            state.SetCooldown(DateTime.UtcNow + PostBattleCooldown);
            state.ClearBattle();

            await _playerRepo.SavePlayer(player, cancellationToken);

            return true;
        }

        private async Task AbandonBattle(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            var elapsedMs = (int)(DateTime.UtcNow - state.BattleStartTime).TotalMilliseconds;

            // No active battle (nothing to resolve) or no elapsed window to re-simulate against — clear
            // and return without recording an outcome or persisting.
            if (elapsedMs <= 0 || !TryResolveActiveBattle(state, out var enemy, out var result, elapsedMs))
            {
                state.ClearBattle();
                return;
            }

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

            await _playerRepo.SavePlayer(player, cancellationToken);
        }

        // Books a victory: grants the earned exp and records the win (kills, zone clears, and the
        // challenge rewards driven off BattleCompletedEvent). Shared by the explicit victory path and
        // the won-abandon path so the two cannot drift — a battle that resolved as a win always pays
        // out the same way, regardless of how the battle was ended.
        //
        // Anti-cheat note: the two callers gate this payout differently and that asymmetry is intentional.
        // EndBattleVictory validates the *client-supplied* claimed timestamp (within one logical tick of the
        // simulated earliest defeat, and not in the future) before paying out. The won-abandon path performs no such
        // timestamp check because it re-simulates capped at the *server-measured* elapsed wall-clock time
        // (AbandonBattle's elapsedMs) — a win only resolves if the enemy died within time the server itself
        // observed, so the server-measured cap is the (stronger) control there and a client timestamp adds
        // nothing. Both paths therefore require a server-validated timeline; neither can be claimed early.
        private DefeatRewards RecordVictory(Player player, CoreEnemy enemy, BattleResult result, PlayerState state)
        {
            // Measure the player's power for the reward from the same frozen snapshot the battle was simulated
            // against, not the live aggregate. Valid mid-battle socket commands (stat reallocation, gear swaps)
            // can shift live power between battle start and the victory claim — which would both diverge from
            // the fought battle and let a client deflate its power right before claiming to inflate the payout.
            // RecordVictory only runs after TryResolveActiveBattle has confirmed an active snapshot, so a null
            // here is a broken invariant rather than a reachable state.
            if (state.Snapshot is not { } snapshot)
            {
                throw new InvalidOperationException("Cannot record a victory without an active battle snapshot.");
            }

            var rewards = new DefeatRewards(snapshot.GetModifiers(_items.GetItem, _itemMods.GetItemMod), enemy);

            player.GrantExp(rewards.ExpReward);
            player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId);

            return rewards;
        }

        // Whether a zone is unlocked for the player. An ungated zone is always open and pays no read cost;
        // a gated zone costs one indexed completion lookup, incurred only on a real zone transition or a
        // boss challenge (not per idle tick). The unlock rule itself lives on the domain Zone.
        private async Task<bool> IsZoneUnlocked(int playerId, CoreZone zone, CancellationToken cancellationToken = default)
        {
            if (zone.UnlockChallengeId is null)
            {
                return true;
            }

            var completedChallengeIds = await _progressRepo.GetCompletedChallengeIds(playerId, cancellationToken);
            return zone.IsUnlocked(completedChallengeIds);
        }

        // Generates the simulation RNG seed from a cryptographic (non-time) entropy source. A wall-clock seed
        // (DateTime.Ticks) is monotonic and low-entropy in its low 32 bits — correlated and predictable between
        // battles — which makes it unsuitable as the shared starting point for the parity-identical crit/dodge
        // RNG (#178). The seed is server-generated and transmitted to the client as-is, so changing the source
        // does not affect how it is consumed. Shared by both start paths.
        internal static uint CreateBattleSeed() => BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));

        // Shared anti-cheat preamble for the three battle-end paths: guards that a battle is active, resolves
        // the snapshotted enemy, and replays the fight once. Returns false (with no outputs) when there is no
        // active battle, so each caller keeps only its outcome-specific branching. Centralising the guard, the
        // ?? 1 / ?? [] fallbacks, and the simulate call here keeps the replay surface from silently drifting.
        private bool TryResolveActiveBattle(
            PlayerState state,
            [MaybeNullWhen(false)] out CoreEnemy enemy,
            [MaybeNullWhen(false)] out BattleResult result,
            int? maxMs = null)
        {
            if (state.ActiveEnemyId is not int enemyId || state.Snapshot is null)
            {
                enemy = default;
                result = default;
                return false;
            }

            var level = state.ActiveEnemyLevel ?? 1;
            var enemySkillIds = state.ActiveEnemySkillIds ?? [];

            enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            // The seed is set alongside the snapshot at battle start (and cleared together), so a non-null
            // snapshot here guarantees a non-null seed — the ?? 0 mirrors the defensive fallbacks above.
            result = SimulateBattle(enemy, enemySkillIds, state.Snapshot, state.BattleSeed ?? 0, maxMs);
            return true;
        }

        private BattleResult SimulateBattle(CoreEnemy enemy, IReadOnlyList<int> enemySkillIds, BattleSnapshot snapshot, uint seed, int? maxMs = null)
        {
            enemy.SetBattleSkills(enemySkillIds);

            var playerBattler = snapshot.ToBattler(_items.GetItem, _itemMods.GetItemMod, _skills.GetSkill);
            var enemyBattler = new Battler(
                new AttributeCollection(enemy.GetAttributeModifiers()),
                enemy.BattleSkills,
                enemy.Level);

            // The same seed shipped to the client at battle start, so the server's anti-cheat replay draws
            // the crit/dodge/block rolls from the identical RNG stream the client simulated.
            var simulator = new BattleSimulator(playerBattler, enemyBattler, seed);
            return simulator.Simulate(maxMs);
        }
    }

    public class BattleStartResult
    {
        public required CoreEnemy Enemy { get; set; }
        public required uint Seed { get; set; }
    }
}
