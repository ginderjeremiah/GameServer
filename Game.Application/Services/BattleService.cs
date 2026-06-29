using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Battle.Offline;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Progress;
using Microsoft.Extensions.Logging;
using CoreClass = Game.Core.Classes.Class;
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
        IProficiencies proficiencies,
        IClasses classes,
        BattleFactory battleFactory,
        OfflineProgressSimulator offlineSimulator,
        ChallengeRewardService challengeRewards,
        ProficiencyRewardService proficiencyRewards,
        ILogger<BattleService> logger)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEnemies _enemies = enemies;
        private readonly IZones _zones = zones;
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly IItems _items = items;
        private readonly IItemMods _itemMods = itemMods;
        private readonly ISkills _skills = skills;
        private readonly IProficiencies _proficiencies = proficiencies;
        private readonly IClasses _classes = classes;
        private readonly BattleFactory _battleFactory = battleFactory;
        private readonly OfflineProgressSimulator _offlineSimulator = offlineSimulator;
        private readonly ChallengeRewardService _challengeRewards = challengeRewards;
        private readonly ProficiencyRewardService _proficiencyRewards = proficiencyRewards;
        private readonly ILogger<BattleService> _logger = logger;

        // Offline-rewards window bounds (spike #879). Below the minimum, a return is treated as no time away
        // (no rewards, just re-anchor); above the cap, only the cap is ever simulated so a long absence (or an
        // all-draw zone) can't run unbounded CPU.
        public static readonly TimeSpan MinimumOfflineAway = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan MaximumOfflineSimulation = TimeSpan.FromHours(10);

        // CPU-waste guard handed to the offline simulator: an opening batch of pure draws is a stalemate the
        // player can neither win nor lose, so simulating the whole away budget of maximum-duration draws earns
        // nothing for the most work. Stop after this many opening all-draw battles (a true stalemate draws
        // every time, so a small count is decisive; any win or loss disables the guard).
        private const int StalemateCutoffBattles = 10;

        // Slack on the server-measured elapsed-time victory check: one logical tick, because the frontend's
        // battle-start may sit up to a tick off the backend's (a battle started mid-tick counts its first
        // partial tick as a full one). A victory claim is rejected only if measurably less server time has
        // elapsed since battle start than the replay's duration minus this slack — i.e. it could not yet have
        // finished. The check is purely server-clock-based, so it is immune to client/server clock skew.
        private static readonly TimeSpan ElapsedBattleTimeTolerance = TimeSpan.FromMilliseconds(GameConstants.MsPerTick);

        // Post-battle enemy cooldown, shared by the win and loss paths so the two cannot diverge. Both anchor
        // it to the server clock; the win path anchors to the battle's completion (battle start + replayed
        // duration) and the loss path to the moment of the loss, but the duration is identical.
        private static readonly TimeSpan PostBattleCooldown = TimeSpan.FromSeconds(5);

        public async Task<BattleStartResult> StartBattle(Player player, PlayerState state, int zoneId, int? newZoneId = null, DateTime? scheduledStartTime = null, int? clientBattleMs = null, CancellationToken cancellationToken = default)
        {
            if (state.HasActiveBattle)
            {
                await AbandonBattle(player, state, clientBattleMs, cancellationToken);
            }

            // A real zone change is gated on the target being unlocked and in circulation (anti-cheat). A
            // legitimate client never navigates into a locked or retired zone — the UI gates both — so such a
            // target is ignored and the battle simply proceeds in the player's current zone. Same-zone
            // re-requests skip the check (and the redundant save) entirely.
            if (newZoneId.HasValue && newZoneId.Value != player.CurrentZoneId)
            {
                var targetZone = _zones.GetDomainZone(newZoneId.Value);
                if (!_zones.IsZoneRetired(newZoneId.Value) && await IsZoneUnlocked(player.Id, targetZone, cancellationToken))
                {
                    player.ChangeZone(newZoneId.Value);
                    zoneId = newZoneId.Value;
                    await _playerRepo.SavePlayer(player, cancellationToken);
                }
            }

            // Lazy relocation: if the resolved zone is no longer viable (it was retired, or every enemy
            // assigned to it has been retired), move the player to the nearest viable zone so the idle loop
            // never stalls on a non-navigable zone or throws spawning from an empty table.
            zoneId = await EnsureViableZone(player, zoneId, cancellationToken);

            var zone = _zones.GetDomainZone(zoneId);

            // Anchor the battle's start to the scheduled time when prefetching the next idle battle during
            // the post-battle cooldown (its deterministic expiry); otherwise to now. Anchoring a prefetched
            // battle to its scheduled start — not now — keeps the elapsed-time victory check and the
            // following cooldown correct (see PrepareNextIdleBattle).
            var battleStartTime = scheduledStartTime ?? DateTime.UtcNow;
            var seed = CreateBattleSeed();

            var enemy = _battleFactory.CreateBattleEnemy(
                zone,
                level => _enemies.GetRandomDomainEnemy(zone.Id, level));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = BattleSnapshot.FromPlayer(player, await CaptureProficiencyLevels(player.Id, cancellationToken));

            state.SetActiveBattle(enemy.Id, enemy.Level, enemySkillIds, seed, battleStartTime, snapshot, zone.Id, isBossBattle: false);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
            };
        }

        /// <summary>
        /// Prefetches the next idle battle for the bundled idle-loop flow: starts a fresh idle encounter in
        /// the player's current zone, anchoring its <see cref="PlayerState.BattleStartTime"/> to the scheduled
        /// post-battle cooldown expiry (<see cref="PlayerState.EnemyCooldown"/>) rather than now. The cooldown
        /// is deterministic, so the next fight begins exactly when it elapses; anchoring the start to that
        /// scheduled time (a) keeps the elapsed-time victory check correct — network latency only ever delays
        /// the claim past the scheduled completion — and (b) keeps the <em>following</em> cooldown correctly
        /// sized: anchoring to now would back-date the start by the whole cooldown and shrink (or zero) it.
        /// The result rides the battle-end response so the client can begin the next fight the instant the
        /// cooldown elapses, without a separate <c>NewEnemy</c> round-trip. Call only after the battle-end
        /// method has set the cooldown and cleared the resolved battle.
        /// </summary>
        public Task<BattleStartResult> PrepareNextIdleBattle(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            return StartBattle(player, state, player.CurrentZoneId, scheduledStartTime: state.EnemyCooldown, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Best-effort <see cref="PrepareNextIdleBattle"/> for the battle-end commands: on any prefetch failure
        /// it logs and returns <c>null</c> instead of throwing. The victory/loss is already durably credited and
        /// has cleared the in-flight battle on the <see cref="PlayerState"/>, but that resolved state is only
        /// persisted to the session cache <em>after</em> this prefetch. Letting the prefetch throw would strand
        /// the resolved state (the cleared battle is lost), so on reconnect the stale session still shows the
        /// already-credited battle as active and the next <c>StartBattle</c> re-abandons — and thus re-credits —
        /// it. Swallowing here keeps the caller on its path to save the resolved state; the client round-trips
        /// <c>NewEnemy</c> when no next battle is bundled.
        /// </summary>
        public async Task<BattleStartResult?> TryPrepareNextIdleBattle(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            try
            {
                return await PrepareNextIdleBattle(player, state, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Next-idle-battle prefetch failed for player {PlayerId}; returning without a bundled next enemy.",
                    player.Id);
                return null;
            }
        }

        /// <summary>
        /// Starts a deterministic battle against the zone's dedicated boss (the "Challenge Boss" action),
        /// separate from the random idle spawn. The boss is fought at the zone's fixed level with its full
        /// authored skill loadout. Returns <c>null</c> when the zone has no dedicated boss authored. Unlike
        /// <see cref="StartBattle"/> there is no cooldown gate — the boss challenge is always available — and
        /// challenging does not change the player's current zone.
        /// </summary>
        public async Task<BattleStartResult?> StartBossBattle(Player player, PlayerState state, int zoneId, int? clientBattleMs = null, CancellationToken cancellationToken = default)
        {
            var zone = _zones.GetDomainZone(zoneId);

            // Validate the challenge before touching the active battle: abandoning is not a cheap no-op
            // (it force-resolves and persists the current battle), so a challenge against a bossless or
            // locked zone must be a true no-op rather than silently ending the player's in-progress fight.
            if (zone.BossEnemyId is not int bossEnemyId)
            {
                return null;
            }

            // Anti-cheat / out of circulation: a locked or retired zone's boss cannot be challenged. A
            // legitimate client can never be in a locked or retired zone to begin with (the idle loop
            // relocates out of a retired one), so this only blocks tampered requests.
            if (_zones.IsZoneRetired(zoneId) || !await IsZoneUnlocked(player.Id, zone, cancellationToken))
            {
                return null;
            }

            if (state.HasActiveBattle)
            {
                await AbandonBattle(player, state, clientBattleMs, cancellationToken);
            }

            var now = DateTime.UtcNow;
            var seed = CreateBattleSeed();

            var enemy = _battleFactory.CreateBossEnemy(
                zone,
                level => _enemies.GetDomainEnemy(bossEnemyId, level)
                    ?? throw new InvalidOperationException(
                        $"Zone {zone.Id} references boss enemy {bossEnemyId}, which does not exist."));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = BattleSnapshot.FromPlayer(player, await CaptureProficiencyLevels(player.Id, cancellationToken));

            state.SetActiveBattle(enemy.Id, enemy.Level, enemySkillIds, seed, now, snapshot, zone.Id, isBossBattle: true);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
            };
        }

        /// <summary>
        /// Persists the player's active idle-loop mode (idle vs. auto-challenge-boss) to the durable player
        /// aggregate so the offline-rewards simulation can resume the correct loop at next login. The boss is
        /// always the player's current zone's boss — the loop never targets a separate zone — so enabling
        /// validates <see cref="Player.CurrentZoneId"/> as anti-cheat the same way <see cref="StartBossBattle"/>
        /// does (in circulation, unlocked, has a dedicated boss), rejecting (no mutation) otherwise; disabling
        /// is always accepted (returning to idle needs no target). The change rides the existing write-behind
        /// save via the player's core-updated event.
        /// </summary>
        public async Task<bool> SetAutoChallengeBoss(Player player, bool enabled, CancellationToken cancellationToken = default)
        {
            if (!enabled)
            {
                player.SetAutoChallengeBoss(false);
                await _playerRepo.SavePlayer(player, cancellationToken);
                return true;
            }

            // Anti-cheat: a retired, locked, or bossless current zone cannot be boss-farmed. The zone is the
            // player's own CurrentZoneId (not client-supplied), so a tampered client can't farm a zone it
            // isn't in; the meaningful gate is that the current zone actually has a challengeable boss.
            var zoneId = player.CurrentZoneId;
            if (!_zones.ValidateZoneId(zoneId) || _zones.IsZoneRetired(zoneId))
            {
                return false;
            }

            var zone = _zones.GetDomainZone(zoneId);
            if (zone.BossEnemyId is null || !await IsZoneUnlocked(player.Id, zone, cancellationToken))
            {
                return false;
            }

            player.SetAutoChallengeBoss(true);
            await _playerRepo.SavePlayer(player, cancellationToken);
            return true;
        }

        public async Task<DefeatResult?> EndBattleVictory(Player player, PlayerState state, int? clientTotalMs = null, CancellationToken cancellationToken = default)
        {
            if (!TryResolveActiveBattle(state, out var enemy, out var result))
            {
                // No battle to resolve. After the caller's HasActiveBattle gate this means a torn state
                // (an enemy id set without its snapshot), which the set/clear invariant should prevent.
                _logger.LogWarning(
                    "EndBattleVictory rejected for player {PlayerId}: no resolvable active battle "
                    + "(activeEnemyId: {ActiveEnemyId}, hasSnapshot: {HasSnapshot}).",
                    player.Id, state.ActiveEnemyId, state.Snapshot is not null);
                return null;
            }

            // Diagnostic only (not anti-cheat): the client reports the battle duration it simulated, so a
            // divergence from the server's parity replay is visible even when the claim still resolves as a
            // win. Logged regardless of the outcome below; absent (null) when not reported.
            if (clientTotalMs is int reportedMs && reportedMs != result.TotalMs)
            {
                _logger.LogWarning(
                    "EndBattleVictory battle-duration divergence for player {PlayerId}: client reported "
                    + "{ClientTotalMs}ms but server replay was {ServerTotalMs}ms (delta: {DeltaMs}, "
                    + "enemyId: {EnemyId}, enemyLevel: {EnemyLevel}, seed: {Seed}).",
                    player.Id, reportedMs, result.TotalMs, reportedMs - result.TotalMs,
                    enemy.Id, enemy.Level, state.BattleSeed);
            }

            if (!result.Victory)
            {
                // The server's parity replay of the exact reported battle did not end in a win — a
                // client/server battle-logic divergence or a forged claim. Seed + enemy + level reproduce it.
                _logger.LogWarning(
                    "EndBattleVictory rejected for player {PlayerId}: server replay was not a victory "
                    + "(enemyId: {EnemyId}, enemyLevel: {EnemyLevel}, seed: {Seed}, playerDied: {PlayerDied}, "
                    + "replayMs: {ReplayMs}, isBoss: {IsBoss}, zoneId: {ZoneId}).",
                    player.Id, enemy.Id, enemy.Level, state.BattleSeed, result.PlayerDied,
                    result.TotalMs, state.IsBossBattle, state.BattleZoneId);
                return null;
            }

            var now = DateTime.UtcNow;
            var battleCompletedAt = state.BattleStartTime.AddMilliseconds(result.TotalMs);

            // Anti-cheat, server-clock only: a victory cannot be claimed before enough real server time has
            // elapsed since battle start for the battle to have actually finished. Network latency only ever
            // delays the claim, so a legitimate win always lands at or after the completion time; reject only
            // when the server itself observed measurably less elapsed time than the replay's duration (minus a
            // one-tick slack for the mid-tick battle-start alignment). Both ends use the server clock, so this
            // is immune to client/server clock skew — unlike a check against a client-supplied timestamp.
            if (now < battleCompletedAt - ElapsedBattleTimeTolerance)
            {
                _logger.LogWarning(
                    "EndBattleVictory rejected for player {PlayerId}: claimed before the battle could finish "
                    + "(battleStart: {BattleStart:O}, replayMs: {ReplayMs}, battleCompletedAt: {BattleCompletedAt:O}, "
                    + "now: {Now:O}, shortByMs: {ShortByMs}, toleranceMs: {ToleranceMs}).",
                    player.Id, state.BattleStartTime, result.TotalMs, battleCompletedAt, now,
                    (battleCompletedAt - now).TotalMilliseconds, ElapsedBattleTimeTolerance.TotalMilliseconds);
                return null;
            }

            var rewards = RecordVictory(player, enemy, result, state, now);

            // Anchor the post-battle cooldown to the battle's server-computed completion, not to now: the gap
            // between completion and now is the post-victory network latency, so subtracting it keeps the idle
            // farm rate latency-independent (a laggy player isn't penalised) without trusting the client clock.
            state.SetCooldown(battleCompletedAt + PostBattleCooldown);
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

            var now = DateTime.UtcNow;
            player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId, now);

            state.SetCooldown(now + PostBattleCooldown);
            state.ClearBattle();

            await _playerRepo.SavePlayer(player, cancellationToken);

            return true;
        }

        /// <summary>
        /// Resolves a stale in-flight battle left by a mid-battle disconnect, crediting it exactly like an
        /// abandon (re-simulate capped at the elapsed wall-clock, pay a win out in full, else record the
        /// loss/draw) and clearing it. A no-op when no battle is active. Used by the offline-rewards flow to
        /// settle the disconnected battle before it simulates the away window — so the window's simulation, and
        /// the idle loop that follows the welcome-back gate, both start from a clean state.
        /// </summary>
        public Task ResolveStaleBattle(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            // No client elapsed for an offline/disconnect resolution — the abandon falls back to wall-clock
            // (capped at DefaultMaxBattleMs), exactly as before.
            return AbandonBattle(player, state, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Computes how long the player was away, replays the missed idle/boss battles (spike #879), applies
        /// the accumulated rewards, and returns a welcome-back summary. Runs inline within the calling socket
        /// command (the cap keeps the worst case well under the command timeout); the command's cancellation
        /// token is threaded into the simulation loop so a long run unwinds promptly.
        /// <para>
        /// Away time is <c>now − <see cref="Player.LastActivity"/></c>, measured server-side. Below
        /// <see cref="MinimumOfflineAway"/> it is a no-op beyond re-anchoring <c>LastActivity</c> (so a second
        /// immediate call earns nothing); otherwise it resolves any stale in-flight battle, resumes the
        /// persisted loop mode in the player's (viability-checked) current zone, simulates up to
        /// <see cref="MaximumOfflineSimulation"/>, applies exp per victory and the consolidated
        /// statistics/challenges, re-anchors <c>LastActivity</c>, and persists once.
        /// </para>
        /// </summary>
        public Task<OfflineProgressSummary> SimulateOfflineProgress(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            return SimulateProgress(player, state, MinimumOfflineAway, cancellationToken);
        }

        /// <summary>
        /// Credits a character being switched away from in a deliberate in-game character switch (spike #922):
        /// the same elapsed-time replay as <see cref="SimulateOfflineProgress"/> but with the
        /// <see cref="MinimumOfflineAway"/> floor dropped, so any elapsed time since the departed character's
        /// <see cref="Player.LastActivity"/> is credited (the 5-minute floor is a login-time concern). Resolves
        /// the in-flight battle, applies the rewards, re-anchors <c>LastActivity</c>, and persists — so the
        /// departed character loses no idle progress when the player switches to another of their characters.
        /// The caller must invoke this off the departed character's battle loop (its socket torn down first),
        /// so the player-state write cannot race a live battle-completion command.
        /// </summary>
        public Task<OfflineProgressSummary> SimulateSwitchProgress(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            return SimulateProgress(player, state, TimeSpan.Zero, cancellationToken);
        }

        // Shared elapsed-time replay for both the login welcome-back path and the deliberate-switch credit. The
        // only difference is the away floor: the login path skips a sub-5-minute return, while a switch credits
        // any elapsed time (minimumAway == zero), so the two cannot otherwise drift.
        private async Task<OfflineProgressSummary> SimulateProgress(Player player, PlayerState state, TimeSpan minimumAway, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var awayMs = (long)(now - player.LastActivity).TotalMilliseconds;
            var cappedAwayMs = Math.Min(awayMs, (long)MaximumOfflineSimulation.TotalMilliseconds);

            // Below the threshold there are no offline rewards. Re-anchor LastActivity (so the next away period
            // starts fresh and an immediate re-claim is a no-op) and return an empty summary. Any stale
            // in-flight battle is left for the idle loop's first StartBattle to abandon, exactly as on a normal
            // reconnect — there is no away window to simulate, so settling it here would change nothing.
            if (awayMs < minimumAway.TotalMilliseconds)
            {
                player.StampActivity(now);
                await _playerRepo.SavePlayer(player, cancellationToken);
                return OfflineProgressSummary.Empty(cappedAwayMs, player.AutoChallengeBoss, player.CurrentZoneId);
            }

            // Settle the disconnected battle before simulating the away window, so its outcome is credited once
            // (here) rather than being re-abandoned by the first live StartBattle after the gate.
            await ResolveStaleBattle(player, state, cancellationToken);

            var (mode, zone) = await ResolveOfflineLoop(player, cancellationToken);
            var proficiencyLevels = await CaptureProficiencyLevels(player.Id, cancellationToken);
            var parameters = BuildSimulationParameters(player, mode, zone, awayMs, proficiencyLevels);

            var result = _offlineSimulator.Simulate(parameters, cancellationToken);

            var levelBefore = player.Level;
            var statPointsBefore = player.StatPoints.StatPointsGained;
            var rewards = await ApplyOfflineRewards(player, result, cancellationToken);

            // Re-anchor the away clock and persist the player (exp/levels/unlocks) in one save. The exp batch
            // already raised its own single core update in ApplyOfflineRewards; this re-anchor raises one more.
            // Both are absolute write-behind writes (the final state persists regardless), and two is still
            // nowhere near the per-victory flood decision 6 avoids.
            player.StampActivity(now);
            await _playerRepo.SavePlayer(player, cancellationToken);

            return new OfflineProgressSummary
            {
                AwayMs = cappedAwayMs,
                AutoChallengeBoss = result.IsBossBattle,
                ZoneId = result.ZoneId,
                BattlesWon = result.Wins,
                BattlesLost = result.Losses,
                BattlesDrawn = result.Draws,
                TotalExp = result.TotalExp,
                LevelsGained = player.Level - levelBefore,
                StatPointsGained = player.StatPoints.StatPointsGained - statPointsBefore,
                CompletedChallenges = rewards.CompletedChallenges,
                ProficiencyGains = rewards.ProficiencyGains.Results,
                OpenedProficiencies = rewards.ProficiencyGains.Opened,
            };
        }

        // Resolves which loop the offline simulation resumes and the zone it runs in. Boss mode resumes only
        // when the persisted flag is set and the current zone still has a challengeable boss (in circulation,
        // unlocked, boss authored); otherwise the loop falls back to idle in the nearest viable zone (the same
        // lazy-relocation the live idle loop uses, so a retired/empty current zone never stalls the sim).
        private async Task<(OfflineLoopMode Mode, CoreZone Zone)> ResolveOfflineLoop(Player player, CancellationToken cancellationToken)
        {
            var currentZoneId = player.CurrentZoneId;
            if (player.AutoChallengeBoss
                && _zones.ValidateZoneId(currentZoneId)
                && !_zones.IsZoneRetired(currentZoneId))
            {
                var bossZone = _zones.GetDomainZone(currentZoneId);
                if (bossZone.BossEnemyId is not null && await IsZoneUnlocked(player.Id, bossZone, cancellationToken))
                {
                    return (OfflineLoopMode.Boss, bossZone);
                }
            }

            var idleZoneId = await EnsureViableZone(player, currentZoneId, cancellationToken);
            return (OfflineLoopMode.Idle, _zones.GetDomainZone(idleZoneId));
        }

        // Builds the simulator inputs for the resolved loop. Idle rolls a random per-zone spawn each battle;
        // boss builds the zone's dedicated boss deterministically — the same factory resolvers the live battle
        // start uses, so an offline battle is constructed identically to an online one. The player snapshot and
        // the catalog resolvers are shared across the whole run (the player's power is stationary offline).
        private OfflineSimulationParameters BuildSimulationParameters(
            Player player, OfflineLoopMode mode, CoreZone zone, long awayMs,
            IReadOnlyList<ProficiencyLevelSnapshot> proficiencyLevels)
        {
            Func<int, CoreEnemy> resolveEnemy = mode == OfflineLoopMode.Boss
                ? BossEnemyResolver(zone)
                : level => _enemies.GetRandomDomainEnemy(zone.Id, level);

            return new OfflineSimulationParameters
            {
                // One snapshot drives the whole window: the player's power — proficiency levels included — is
                // frozen at the window start, so the away period fights at a stationary power even as the
                // simulated victories accrue proficiency XP (mirroring how gear and stats are frozen).
                Snapshot = BattleSnapshot.FromPlayer(player, proficiencyLevels),
                Mode = mode,
                Zone = zone,
                AwayBudgetMs = awayMs,
                CapMs = (long)MaximumOfflineSimulation.TotalMilliseconds,
                CooldownMs = (int)PostBattleCooldown.TotalMilliseconds,
                ResolveEnemy = resolveEnemy,
                ResolveItem = _items.GetItem,
                ResolveMod = _itemMods.GetItemMod,
                ResolveSkill = _skills.GetSkill,
                ResolveProficiency = _proficiencies.GetProficiency,
                ResolveClass = ResolveClass,
                SeedSource = CreateBattleSeed,
                StalemateCutoffBattles = StalemateCutoffBattles,
            };
        }

        // The boss resolver for a boss-mode run: the zone's dedicated boss at the requested (fixed boss) level.
        // ResolveOfflineLoop only selects boss mode for a zone whose BossEnemyId is set, so the null check here
        // is a defensive invariant rather than a reachable state.
        private Func<int, CoreEnemy> BossEnemyResolver(CoreZone zone)
        {
            var bossEnemyId = zone.BossEnemyId
                ?? throw new InvalidOperationException($"Boss offline loop for zone {zone.Id} has no boss enemy.");
            return level => _enemies.GetDomainEnemy(bossEnemyId, level)
                ?? throw new InvalidOperationException(
                    $"Zone {zone.Id} references boss enemy {bossEnemyId}, which does not exist.");
        }

        // Applies a simulated away window's rewards to the player and progress in one consolidated pass
        // (spike #879 decisions 6 & 7): each battle feeds the same per-battle statistics path the live handler
        // uses, exp is granted per victory (so the per-grant clamp never truncates a haul and levels accrue),
        // and the affected challenges are evaluated once at the end with the live per-challenge push suppressed
        // (the summary is the notification). Returns the completed challenges and the folded proficiency gains
        // (spike #982 decision 9 — the offline accrual's notification rides the summary, not a per-battle push).
        private async Task<OfflineRewards> ApplyOfflineRewards(Player player, OfflineProgressResult result, CancellationToken cancellationToken)
        {
            if (result.BattlesSimulated == 0)
            {
                return OfflineRewards.Empty;
            }

            var progress = await _progressRepo.Load(player, cancellationToken);

            var victoryExpRewards = new List<int>();
            var proficiencyGains = new ProficiencyGainAccumulator();
            // Union the statistic rows touched across every battle, so the end-of-window challenge evaluation
            // sees every moved statistic. RecordBattleCompleted currently returns the progress aggregate's
            // cumulative dirty set (it is loaded once for the whole window), but unioning each call's result
            // explicitly keeps this correct even if that method were ever changed to return only the per-battle
            // delta — a mixed-outcome window can touch a tracked statistic in a non-final battle (e.g. a kill
            // challenge crossed by early wins before a closing loss), and the union captures it regardless.
            var touchedStatistics = new HashSet<(EStatisticType Type, int? EntityId)>();
            foreach (var battle in result.Battles)
            {
                foreach (var key in progress.RecordBattleCompleted(
                    battle.Enemy, battle.Result.Victory, battle.Result.PlayerDied, battle.Result.TotalMs,
                    battle.Result.Stats, result.IsBossBattle, result.ZoneId))
                {
                    touchedStatistics.Add(key);
                }

                if (battle.Result.Victory)
                {
                    victoryExpRewards.Add(battle.ExpReward);

                    // Accrue proficiency XP per won battle, exactly as the live handler does — same inputs
                    // (this battle's skill stats + player power), same service — so the offline accrual matches
                    // what the player would have earned live (the "offline == live" invariant). The push is
                    // suppressed; the folded results ride the welcome-back summary instead.
                    proficiencyGains.Add(_proficiencyRewards.AccrueAndApply(
                        progress, battle.Result.Stats, battle.PlayerPower, player, notify: false));
                }
            }

            // Grant the whole window's exp before evaluating challenges, so a statistic-independent
            // LevelReached challenge sees the post-window level (mirroring the live order, where exp is granted
            // before the battle-completed handler evaluates challenges).
            if (victoryExpRewards.Count > 0)
            {
                player.GrantOfflineExp(victoryExpRewards);
            }

            var completed = _challengeRewards.EvaluateAndApply(progress, touchedStatistics, player, notify: false);

            await _progressRepo.Save(progress, cancellationToken);
            return new OfflineRewards(completed, proficiencyGains.Build());
        }

        private async Task AbandonBattle(Player player, PlayerState state, int? clientBattleMs = null, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var wallClockMs = (int)(now - state.BattleStartTime).TotalMilliseconds;

            // Bound the re-simulation by how long the client actually simulated the battle being abandoned,
            // when it reports it — so a battle the client never fought (e.g. a server-prefetched next battle
            // superseded by a zone/build change during the cooldown, reported as 0) records no outcome rather
            // than a phantom one from re-simulating the post-cooldown wall-clock gap. The reported value is
            // never trusted above the real server-elapsed time, so a won-abandon still can't be claimed faster
            // than wall-clock allows (under-reporting only ever resolves to an earlier, not-yet-won state).
            var elapsedMs = clientBattleMs is int reported
                ? Math.Min(Math.Max(reported, 0), wallClockMs)
                : wallClockMs;

            // Clamp the replay window to the battle's maximum duration. A battle never runs past
            // DefaultMaxBattleMs on the client — it ends as a draw at the cap — so the replay must not either:
            // extra time before the next battle starting must not let the re-simulation run past the cap and
            // resolve a reported stalemate into a spurious win or loss.
            var simulateMs = Math.Min(elapsedMs, GameConstants.DefaultMaxBattleMs);

            // No active battle (nothing to resolve) or no elapsed window to re-simulate against — clear
            // and return without recording an outcome or persisting.
            if (elapsedMs <= 0 || !TryResolveActiveBattle(state, out var enemy, out var result, simulateMs))
            {
                state.ClearBattle();
                return;
            }

            if (result.Victory)
            {
                // The enemy died within the (wall-clock-capped) elapsed time the client simulated, so this
                // abandon resolved as a real victory. Pay it out exactly like EndBattleVictory (exp +
                // win/clear/challenge credit) rather than booking the win while silently withholding the
                // earned exp (#206).
                RecordVictory(player, enemy, result, state, now);
            }
            else
            {
                player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId, now);
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
        // internal (not private) so an integration test can assert the live PlayerPower snapshot directly:
        // EndBattleVictory returns only a client-facing DefeatResult, and the BattleStats this mutates is
        // carried on the BattleCompletedEvent, which the dispatcher clears after handling — leaving no other
        // seam to observe that result.Stats.PlayerPower is set from the snapshot rather than the live aggregate.
        internal DefeatRewards RecordVictory(Player player, CoreEnemy enemy, BattleResult result, PlayerState state, DateTime timestamp)
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

            var rewards = new DefeatRewards(
                snapshot.GetModifiersWithSignaturePassive(_items.GetItem, _itemMods.GetItemMod, _proficiencies.GetProficiency, ResolveClass), enemy);

            // Snapshot the player's power onto the battle stats so the proficiency accrual normalizes activity
            // by the identical measure the difficulty curve uses (spike #1318) — captured here from the same
            // snapshot modifiers, not the live aggregate.
            result.Stats.PlayerPower = rewards.PlayerPower;

            player.GrantExp(rewards.ExpReward);
            // Thread the player's power onto the battle-completed event so the progress handler can normalize
            // each path's activity by it for the effect-based proficiency accrual (spike #1318) — the same
            // snapshot-measured power the exp reward above used.
            player.RecordBattleCompleted(
                enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId, timestamp,
                rewards.PlayerPower);

            return rewards;
        }

        // Whether a zone is viable for the idle loop: in circulation (not retired) and carrying at least one
        // spawnable enemy. The two facts live in separate caches (zone retirement vs the enemy spawn tables),
        // so they are combined here rather than on either lean domain model. The id is range-checked first so
        // a stale/out-of-range CurrentZoneId reads as non-viable (and triggers relocation) instead of throwing.
        private bool IsZoneViable(int zoneId)
        {
            return _zones.ValidateZoneId(zoneId)
                && !_zones.IsZoneRetired(zoneId)
                && _enemies.HasSpawnableEnemies(zoneId);
        }

        // Relocates the player when their resolved zone is no longer viable, returning the zone the battle
        // should run in. "Nearest" is the lowest-Order zone the player has unlocked that is viable, falling
        // back to the starting zone. A no-op (no save) when the current zone is already viable, so the idle
        // hot path pays only the cheap viability check; the completion lookup and scan run only on a relocate.
        private async Task<int> EnsureViableZone(Player player, int zoneId, CancellationToken cancellationToken)
        {
            // An out-of-range zone id is corruption/tampering, not a relocation case: leave it for the
            // downstream GetDomainZone to surface loudly (fail-fast) rather than silently relocating.
            if (!_zones.ValidateZoneId(zoneId) || IsZoneViable(zoneId))
            {
                return zoneId;
            }

            var completedChallengeIds = await _progressRepo.GetCompletedChallengeIds(player.Id, cancellationToken);
            // Filter to viable candidates before ordering, then resolve each domain zone once (lazily, so the
            // resolve stops at the first unlocked match) rather than re-resolving it inside the predicate.
            var destination = _zones.All()
                .Where(zone => IsZoneViable(zone.Id))
                .OrderBy(zone => zone.Order)
                .Select(zone => _zones.GetDomainZone(zone.Id))
                .FirstOrDefault(zone => zone.IsUnlocked(completedChallengeIds));
            var newZoneId = destination?.Id ?? NewPlayerFactory.StartingZoneId;

            if (newZoneId != player.CurrentZoneId)
            {
                player.ChangeZone(newZoneId);
                await _playerRepo.SavePlayer(player, cancellationToken);
            }

            return newZoneId;
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

        // Captures the player's current proficiency levels for the battle snapshot, so the per-level/milestone
        // bonuses bake into the fight at its start (spike #982 area E). Proficiency progress lives on the
        // separate PlayerProgress aggregate, so it is read through the lean proficiency-only accessor rather
        // than the battle aggregate; the empty list (the universal state until proficiencies are authored and
        // opened) yields no proficiency modifiers, so the replay stays identical to today.
        private async Task<List<ProficiencyLevelSnapshot>> CaptureProficiencyLevels(int playerId, CancellationToken cancellationToken)
        {
            var proficiencies = await _progressRepo.GetProficiencies(playerId, cancellationToken);
            return proficiencies
                .Select(p => new ProficiencyLevelSnapshot { ProficiencyId = p.ProficiencyId, Level = p.Level })
                .ToList();
        }

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

        // Resolves the player's class for the locked-base distribution, failing loudly on an unresolvable id
        // (a content-data mistake) rather than silently dropping the attribute fingerprint — the player-load
        // missing-reference policy applied to the battle-assembly path.
        private CoreClass ResolveClass(int classId) =>
            _classes.GetClass(classId)
            ?? throw new InvalidOperationException($"Class {classId} could not be resolved from the catalogue.");

        private BattleResult SimulateBattle(CoreEnemy enemy, IReadOnlyList<int> enemySkillIds, BattleSnapshot snapshot, uint seed, int? maxMs = null)
        {
            enemy.SetBattleSkills(enemySkillIds);

            var playerBattler = snapshot.ToBattler(
                _items.GetItem, _itemMods.GetItemMod, _skills.GetSkill, _proficiencies.GetProficiency, ResolveClass);
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

    /// <summary>
    /// The welcome-back summary of a returning player's offline progress: how long they were away (capped),
    /// which loop ran, the battle tally, and the rewards earned (exp, levels, stat points, and the challenges
    /// completed with what they unlocked). Returned by <see cref="BattleService.SimulateOfflineProgress"/> and
    /// projected to the API model the client gate renders.
    /// </summary>
    public class OfflineProgressSummary
    {
        /// <summary>How long the player was away, in milliseconds, clamped to the simulation cap.</summary>
        public required long AwayMs { get; init; }

        /// <summary>Whether the simulated loop was auto-challenging the boss (<c>true</c>) or idle-farming
        /// (<c>false</c>).</summary>
        public required bool AutoChallengeBoss { get; init; }

        /// <summary>The zone the loop ran in.</summary>
        public required int ZoneId { get; init; }

        public required int BattlesWon { get; init; }
        public required int BattlesLost { get; init; }
        public required int BattlesDrawn { get; init; }

        /// <summary>Total experience earned across all victories.</summary>
        public required long TotalExp { get; init; }

        /// <summary>Levels gained over the window.</summary>
        public required int LevelsGained { get; init; }

        /// <summary>Stat points gained over the window (from the levels gained).</summary>
        public required int StatPointsGained { get; init; }

        /// <summary>The challenges completed over the window, each with the reward ids it unlocked.</summary>
        public required IReadOnlyList<CompletedChallenge> CompletedChallenges { get; init; }

        /// <summary>The proficiency gains accrued over the window, folded across every won battle: each trained
        /// proficiency's total XP gained, its final level/residual XP, the milestones it crossed, and the reward
        /// skills granted (spike #982 decision 9 — the offline accrual's notification rides this summary).</summary>
        public required IReadOnlyList<ProficiencyXpResult> ProficiencyGains { get; init; }

        /// <summary>The proficiency nodes opened over the window (a maxed tier's next tier or a newly-satisfied
        /// gateway), each with the seed skill it granted (if any).</summary>
        public required IReadOnlyList<ProficiencyOpened> OpenedProficiencies { get; init; }

        /// <summary>
        /// Whether the window produced anything worth gating on. The frontend skips the welcome-back gate for
        /// an empty summary (a sub-threshold absence, or one that earned nothing) and enters the game directly.
        /// A window that only advanced proficiencies (e.g. a maxed-XP-level character) still reports progress.
        /// </summary>
        public bool HasProgress =>
            BattlesWon > 0 || BattlesLost > 0 || BattlesDrawn > 0
            || CompletedChallenges.Count > 0 || ProficiencyGains.Count > 0 || OpenedProficiencies.Count > 0;

        /// <summary>An empty summary: away time recorded but nothing simulated (a sub-threshold return).</summary>
        public static OfflineProgressSummary Empty(long awayMs, bool autoChallengeBoss, int zoneId) => new()
        {
            AwayMs = awayMs,
            AutoChallengeBoss = autoChallengeBoss,
            ZoneId = zoneId,
            BattlesWon = 0,
            BattlesLost = 0,
            BattlesDrawn = 0,
            TotalExp = 0,
            LevelsGained = 0,
            StatPointsGained = 0,
            CompletedChallenges = [],
            ProficiencyGains = [],
            OpenedProficiencies = [],
        };
    }

    /// <summary>
    /// The rewards a simulated away window applied, returned by <see cref="BattleService"/>'s offline-rewards
    /// pass: the challenges completed and the folded proficiency gains (XP/levels/milestones/skills) plus opened
    /// nodes. Both feed the welcome-back summary; the per-challenge and per-battle live pushes are suppressed.
    /// </summary>
    public record OfflineRewards(
        IReadOnlyList<CompletedChallenge> CompletedChallenges,
        ProficiencyAccrualResult ProficiencyGains)
    {
        /// <summary>No rewards: nothing was simulated in the window.</summary>
        public static OfflineRewards Empty { get; } = new([], ProficiencyAccrualResult.Empty);
    }
}
