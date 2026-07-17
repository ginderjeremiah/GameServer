using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Battle.Offline;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Progress;
using Microsoft.Extensions.Logging;
using CoreClass = Game.Core.Classes.Class;
using CoreEnemy = Game.Core.Enemies.Enemy;

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
        ZoneResolutionService zoneResolution,
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
        private readonly ZoneResolutionService _zoneResolution = zoneResolution;
        private readonly ILogger<BattleService> _logger = logger;

        // Slack on the server-measured elapsed-time victory check: one logical tick, because the frontend's
        // battle-start may sit up to a tick off the backend's (a battle started mid-tick counts its first
        // partial tick as a full one). A victory claim is rejected only if measurably less server time has
        // elapsed since battle start than the replay's duration minus this slack — i.e. it could not yet have
        // finished. The check is purely server-clock-based, so it is immune to client/server clock skew.
        private static readonly TimeSpan ElapsedBattleTimeTolerance = TimeSpan.FromMilliseconds(GameConstants.MsPerTick);

        // Post-battle enemy cooldown, shared by the win and loss paths so the two cannot diverge. Both anchor
        // it to the server clock; the win path anchors to the battle's completion (battle start + replayed
        // duration) and the loss path to the moment of the loss, but the duration is identical. Internal (not
        // private): OfflineProgressService's simulated loop uses the identical cooldown between battles so the
        // offline pass paces the same as the live idle loop.
        internal static readonly TimeSpan PostBattleCooldown = TimeSpan.FromSeconds(5);

        public async Task<BattleStartResult> StartBattle(Player player, PlayerState state, int zoneId, int? newZoneId = null, DateTime? scheduledStartTime = null, int? clientBattleMs = null, bool forceAbandon = false, CancellationToken cancellationToken = default)
        {
            // Non-null only when the abandoned battle resolved to a win/loss/draw (AbandonBattle just applied
            // the post-battle cooldown to state.EnemyCooldown, #1851) — used below to anchor the replacement
            // battle to that cooldown's expiry instead of now, exactly like PrepareNextIdleBattle does for the
            // natural end-of-battle flow. Otherwise this abandon-then-respawn round trip would let a caller
            // (NewEnemy) skip the cooldown it just incurred by spawning the next battle immediately.
            DateTime? abandonedOutcomeCooldown = null;

            if (state.HasActiveBattle)
            {
                var (handoff, _) = await AbandonBattle(player, state, clientBattleMs, cancellationToken: cancellationToken);
                if (handoff is not null && !forceAbandon)
                {
                    // A still-in-progress handoff (#1595) means the existing battle hasn't concluded yet —
                    // hand it back instead of abandoning it for a fresh spawn, unless the caller explicitly
                    // asked to force-discard it (forceAbandon) — the same override StartBossBattle always
                    // applies for ChallengeBoss (#1690). Nothing was cleared or persisted for a
                    // still-in-progress abandon, so proceeding to overwrite it below is safe either way.
                    return handoff;
                }

                // Callers only reach StartBattle with an already-active battle via NewEnemy, which gates on
                // state.IsOnCooldown before calling in, so a pre-existing cooldown can never still be in the
                // future here — a future EnemyCooldown at this point can only be the one AbandonBattle just set.
                if (handoff is null && state.EnemyCooldown > DateTime.UtcNow)
                {
                    abandonedOutcomeCooldown = state.EnemyCooldown;
                }
            }

            // A real zone change is gated on the target being in range, unlocked, in circulation, and a combat
            // zone (anti-cheat). A legitimate client never navigates a battle into an out-of-range, locked,
            // retired, or Home zone — the UI gates all four (and never spawns a battle in Home at all) — so
            // such a target is ignored and the battle simply proceeds in the player's current zone. Refusing
            // the Home target keeps the "fake zone" invariant the offline path relies on: a player's persisted
            // CurrentZoneId is never the no-combat Home sanctuary, so offline rewards always replay their last
            // real combat zone. Same-zone re-requests skip the check (and the redundant save) entirely.
            if (newZoneId.HasValue && newZoneId.Value != player.CurrentZoneId && _zones.ValidateZoneId(newZoneId.Value))
            {
                var targetZone = _zones.GetDomainZone(newZoneId.Value);
                if (!_zones.IsZoneRetired(newZoneId.Value)
                    && !_zones.IsHomeZone(newZoneId.Value)
                    && await _zoneResolution.IsZoneUnlocked(player.Id, targetZone, cancellationToken))
                {
                    player.ChangeZone(newZoneId.Value);
                    zoneId = newZoneId.Value;
                    await _playerRepo.SavePlayer(player, cancellationToken);
                }
            }

            // Lazy relocation: if the resolved zone is no longer viable (it was retired, or every enemy
            // assigned to it has been retired), move the player to the nearest viable zone so the idle loop
            // never stalls on a non-navigable zone or throws spawning from an empty table.
            zoneId = await _zoneResolution.EnsureViableZone(player, zoneId, cancellationToken);

            var zone = _zones.GetDomainZone(zoneId);

            // Anchor the battle's start to the scheduled time when prefetching the next idle battle during
            // the post-battle cooldown (its deterministic expiry), or when this call's own abandon just
            // incurred that cooldown (abandonedOutcomeCooldown); otherwise to now. Anchoring to the scheduled
            // start rather than now keeps the elapsed-time victory check and the following cooldown correct
            // (see PrepareNextIdleBattle) — and, for the abandon case, is what makes the cooldown just applied
            // actually pace the replacement battle instead of being immediately bypassed (#1851).
            var battleStartTime = scheduledStartTime ?? abandonedOutcomeCooldown ?? DateTime.UtcNow;
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
                IsBossBattle = false,
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
        /// <para>
        /// This swallow can reach a <see cref="Game.Abstractions.DataAccess.PlayerPersistenceFlushFailedException"/>
        /// without the socket layer marking the session for reload (#1632): <c>ZoneResolutionService.EnsureViableZone</c>
        /// (called unconditionally from <see cref="StartBattle"/>) saves the player when the prefetch's current
        /// zone has gone non-viable mid-session. That's accepted here — the only event that save can raise is
        /// <c>PlayerCoreUpdatedEvent</c> (the zone change), which self-heals: the next core mutation re-raises it,
        /// and the next battle re-runs <c>EnsureViableZone</c> and re-relocates if it's still lost. No one-shot
        /// unlock event reaches this path today; if a future change routes one through it, this swallow would
        /// silently reopen the #1632 hole and needs revisiting.
        /// </para>
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
            // Validate the whole challenge before touching the active battle: abandoning is not a cheap no-op
            // (it force-resolves and persists the current battle), so a challenge against an out-of-range,
            // bossless, retired, or locked zone must be a true no-op rather than silently ending the player's
            // in-progress fight. A legitimate client can never be in a locked or retired zone to begin with
            // (the idle loop relocates out of a retired one), so those gates only block tampered requests.
            var zone = await _zoneResolution.ResolveChallengeableBossZone(player.Id, zoneId, cancellationToken);
            if (zone is null)
            {
                return null;
            }

            if (state.HasActiveBattle)
            {
                // Deliberate override: challenging the boss always abandons whatever idle battle is running
                // (even one still genuinely in progress, #1595) and proceeds — unlike NewEnemy, this is an
                // explicit different action, not "give my existing battle back," so the handoff is discarded.
                // Nothing was cleared or persisted for a still-in-progress abandon, so overwriting the
                // in-memory state below with the boss battle is safe either way. An abandon that resolves an
                // outcome (win/loss/draw) applies the post-battle cooldown to state.EnemyCooldown (#1851); one
                // that resolves nothing (e.g. mid an already-running cooldown, where elapsedMs clamps to 0)
                // leaves EnemyCooldown exactly as it found it. Either way, the general anchor below picks it up.
                await AbandonBattle(player, state, clientBattleMs, cancellationToken: cancellationToken);
            }

            // Anchor to the in-effect cooldown rather than always to now — ChallengeBoss has no command-level
            // cooldown gate (unlike NewEnemy, gated on IsOnCooldown before it ever reaches StartBattle), so
            // this anchor is the only thing enforcing the pacing the response's Cooldown handshake promises.
            // A cooldown can be running here whether or not this call's own abandon (above) just set it — a
            // normal (non-abandoned) victory/loss from the previous battle, or a mid-cooldown challenge that
            // resolved no outcome, leave EnemyCooldown just as live. Anchoring to its expiry in all of these
            // cases, not only the abandon-resolved one, closes the gap where a scripted ChallengeBoss loop paid
            // only battle duration per kill instead of duration + cooldown (#1920).
            var utcNow = DateTime.UtcNow;
            var battleStartTime = state.IsOnCooldown(utcNow) ? state.EnemyCooldown : utcNow;
            var seed = CreateBattleSeed();

            var enemy = _battleFactory.CreateBossEnemy(zone, _zoneResolution.BossEnemyResolver(zone));

            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var snapshot = BattleSnapshot.FromPlayer(player, await CaptureProficiencyLevels(player.Id, cancellationToken));

            state.SetActiveBattle(enemy.Id, enemy.Level, enemySkillIds, seed, battleStartTime, snapshot, zone.Id, isBossBattle: true);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = seed,
                IsBossBattle = true,
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
            if (await _zoneResolution.ResolveChallengeableBossZone(player.Id, player.CurrentZoneId, cancellationToken) is null)
            {
                return false;
            }

            player.SetAutoChallengeBoss(true);
            await _playerRepo.SavePlayer(player, cancellationToken);
            return true;
        }

        public async Task<DefeatResult?> EndBattleVictory(Player player, PlayerState state, int? clientTotalMs = null, CancellationToken cancellationToken = default)
        {
            if (!TryValidateBattleEndClaim(
                    player, state, expectVictory: true, clientTotalMs, nameof(EndBattleVictory),
                    out var enemy, out var result, out var playerMaterials, out var now, out var battleCompletedAt))
            {
                return null;
            }

            var rewards = RecordVictory(player, enemy, result, state, now, playerMaterials: playerMaterials);

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
                PlayerRating = rewards.PlayerRating,
            };
        }

        public async Task<bool> EndBattleLoss(Player player, PlayerState state, int? clientTotalMs = null, CancellationToken cancellationToken = default)
        {
            if (!TryValidateBattleEndClaim(
                    player, state, expectVictory: false, clientTotalMs, nameof(EndBattleLoss),
                    out var enemy, out var result, out _, out var now, out _))
            {
                return false;
            }

            player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId, now, state.BattleSeed);

            state.SetCooldown(now + PostBattleCooldown);
            state.ClearBattle();

            await _playerRepo.SavePlayer(player, cancellationToken);

            return true;
        }

        // Shared claim-validation pipeline for the two battle-end commands (expectVictory selects
        // EndBattleVictory's vs EndBattleLoss's expected outcome): the idempotency backstop, active-battle
        // resolution, the client/server duration divergence diagnostic, the replay-must-match-the-claimed-
        // outcome rejection, and the elapsed-time anti-cheat gate (#1630). Extracted so the two paths cannot
        // drift from one another the way #1993 and #2016 each had to be hand-mirrored across both (#2048) —
        // each caller keeps only its outcome-specific payout. commandName names the caller in the log
        // messages so a warning is still attributable to EndBattleVictory vs EndBattleLoss.
        //
        // `now` and `battleCompletedAt` are both surfaced to the caller: EndBattleVictory anchors its cooldown
        // to battleCompletedAt (the battle's server-computed completion) while EndBattleLoss anchors to now —
        // an intentional asymmetry (see PostBattleCooldown) that this extraction preserves rather than unifies.
        private bool TryValidateBattleEndClaim(
            Player player,
            PlayerState state,
            bool expectVictory,
            int? clientTotalMs,
            string commandName,
            [MaybeNullWhen(false)] out CoreEnemy enemy,
            [MaybeNullWhen(false)] out BattleResult result,
            out BattlerMaterials? playerMaterials,
            out DateTime now,
            out DateTime battleCompletedAt)
        {
            now = default;
            battleCompletedAt = default;

            // Idempotency backstop (#1874/#1993): a reconnecting client can re-present an already-durably-
            // credited battle directly through this command instead of the natural AbandonBattle reconnect
            // path (see BattleAlreadyCredited), so the same guard applies here before paying for the replay
            // below.
            if (BattleAlreadyCredited(state, player))
            {
                _logger.LogWarning(
                    "{CommandName} rejected for player {PlayerId}: battle seed {Seed} was already "
                    + "credited (stale session re-presenting a resolved battle).",
                    commandName, player.Id, state.BattleSeed);
                state.ClearBattle();
                enemy = default;
                result = default;
                playerMaterials = default;
                return false;
            }

            if (!TryResolveActiveBattle(state, out enemy, out result, out playerMaterials))
            {
                // No battle to resolve. After the caller's HasActiveBattle gate this means a torn state
                // (an enemy id set without its snapshot), which the set/clear invariant should prevent.
                _logger.LogWarning(
                    "{CommandName} rejected for player {PlayerId}: no resolvable active battle "
                    + "(activeEnemyId: {ActiveEnemyId}, hasSnapshot: {HasSnapshot}).",
                    commandName, player.Id, state.ActiveEnemyId, state.Snapshot is not null);
                return false;
            }

            // Diagnostic only (not anti-cheat): the client reports the battle duration it simulated, so a
            // divergence from the server's parity replay is visible even when the claim still resolves as
            // expected. Logged regardless of the outcome below; absent (null) when not reported.
            if (clientTotalMs is int reportedMs && reportedMs != result.TotalMs)
            {
                _logger.LogWarning(
                    "{CommandName} battle-duration divergence for player {PlayerId}: client reported "
                    + "{ClientTotalMs}ms but server replay was {ServerTotalMs}ms (delta: {DeltaMs}, "
                    + "enemyId: {EnemyId}, enemyLevel: {EnemyLevel}, seed: {Seed}).",
                    commandName, player.Id, reportedMs, result.TotalMs, reportedMs - result.TotalMs,
                    enemy.Id, enemy.Level, state.BattleSeed);
            }

            if (result.Victory != expectVictory)
            {
                // The server's parity replay of the exact reported battle did not agree with the claimed
                // outcome — a client/server battle-logic divergence or a forged claim. Seed + enemy + level
                // reproduce it.
                _logger.LogWarning(
                    "{CommandName} rejected for player {PlayerId}: server replay did not match the claimed "
                    + "outcome (enemyId: {EnemyId}, enemyLevel: {EnemyLevel}, seed: {Seed}, playerDied: "
                    + "{PlayerDied}, replayMs: {ReplayMs}, isBoss: {IsBoss}, zoneId: {ZoneId}).",
                    commandName, player.Id, enemy.Id, enemy.Level, state.BattleSeed, result.PlayerDied,
                    result.TotalMs, state.IsBossBattle, state.BattleZoneId);
                return false;
            }

            // Captured after the replay above so the elapsed-time comparison reflects the moment the claim is
            // actually adjudicated (and so `now` is also the timestamp the outcome-specific payout books).
            now = DateTime.UtcNow;

            // Anti-cheat (#1630), server-clock only: a battle-end claim cannot be accepted before enough real
            // server time has elapsed since battle start for the replayed outcome to have actually happened.
            if (ClaimedBeforeBattleCouldFinish(state.BattleStartTime, result.TotalMs, now, out battleCompletedAt))
            {
                _logger.LogWarning(
                    "{CommandName} rejected for player {PlayerId}: claimed before the battle could finish "
                    + "(battleStart: {BattleStart:O}, replayMs: {ReplayMs}, battleCompletedAt: {BattleCompletedAt:O}, "
                    + "now: {Now:O}, shortByMs: {ShortByMs}, toleranceMs: {ToleranceMs}).",
                    commandName, player.Id, state.BattleStartTime, result.TotalMs, battleCompletedAt, now,
                    (battleCompletedAt - now).TotalMilliseconds, ElapsedBattleTimeTolerance.TotalMilliseconds);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves a stale in-flight battle left by a mid-battle disconnect, crediting it exactly like an
        /// abandon (re-simulate capped at the elapsed wall-clock, pay a win out in full, else record the
        /// loss/draw) and clearing it — or, when the replay hasn't concluded within the 2-minute cap, handing
        /// it back still active with its elapsed offset (#1595) instead of booking a phantom draw.
        /// <see cref="StaleBattleResolution.Handoff"/> is null when the battle was resolved (or there was none
        /// to resolve); non-null otherwise. <see cref="StaleBattleResolution.SettledBattleMs"/> carries the
        /// resolved battle's own credited duration (null when nothing was resolved, e.g. a handoff or no active
        /// battle at all) — the caller (the offline-rewards flow) must deduct that span, plus its post-battle
        /// cooldown, from the away window it is about to simulate, since the settled battle's span already
        /// falls inside that same window and must not be credited twice (#1882).
        /// </summary>
        public Task<StaleBattleResolution> ResolveStaleBattle(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            // No client elapsed for an offline/disconnect resolution — the abandon falls back to wall-clock
            // (capped at DefaultMaxBattleMs), exactly as before. Both callers (the login welcome-back gate and
            // the character-switch credit) resolve a player who by construction has no live socket right now,
            // so the outcome is recorded but the live push is suppressed (#1859) — mirroring the offline
            // window's own notify: false, rather than tripping SocketManagerService's no-active-socket warning
            // on every routine switch/reconnect.
            return AbandonBattle(player, state, notify: false, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Hands back the offline crediting loop's pending battle (#1596) as already active, backdated so it
        /// resumes at its true elapsed offset: the away-window boundary fell <em>inside</em> this exact battle
        /// (same enemy/seed the simulator already drew and simulated) rather than after it, so the simulator
        /// left it uncredited — it is not a fresh spawn, it is that same unconcluded fight. Mirrors <see
        /// cref="AbandonBattle"/>'s still-in-progress hand-back (#1595): same-shaped <see cref="BattleStartResult"/>
        /// with a non-null <see cref="BattleStartResult.ElapsedOffsetMs"/>, so the welcome-back gate resumes it
        /// via replay-to-offset (#1597) with no extra round trip. The stored snapshot is <see
        /// cref="OfflinePendingBattle.Snapshot"/> — the one the simulator actually fought this battle against,
        /// not the window-start snapshot the caller's run started from (#1758) — so the server's later
        /// anti-cheat replay agrees with the client, which resumes from the same post-reward state.
        /// </summary>
        internal BattleStartResult HandBackPendingBattle(
            PlayerState state, OfflinePendingBattle pending, int zoneId, bool isBossBattle, DateTime now)
        {
            var enemy = pending.Enemy;
            var enemySkillIds = enemy.BattleSkills.Select(skill => skill.Id).ToList();
            var startTime = now.AddMilliseconds(-pending.ElapsedOffsetMs);

            state.SetActiveBattle(
                enemy.Id, enemy.Level, enemySkillIds, pending.Seed, startTime, pending.Snapshot, zoneId, isBossBattle);

            return new BattleStartResult
            {
                Enemy = enemy,
                Seed = pending.Seed,
                ElapsedOffsetMs = pending.ElapsedOffsetMs,
                IsBossBattle = isBossBattle,
            };
        }

        // Handoff is null when the stale battle was resolved (win/loss/draw, or nothing to resolve) and has
        // already been cleared/persisted. Handoff is a non-null BattleStartResult — the same enemy/seed, plus
        // the real elapsed offset since BattleStartTime — when the battle is instead handed back still active
        // (#1595): the caller must leave PlayerState untouched in that case (nothing was cleared or saved).
        // SettledBattleMs carries the resolved battle's own credited duration (its BattleResult.TotalMs) only
        // when an outcome was actually recorded; null for a handoff and for "nothing to resolve" alike.
        // notify is the live client-push toggle threaded down to the recorded outcome (#1859): StartBattle and
        // StartBossBattle abandon a still-live player's existing battle to start a new one, so they keep the
        // default true; ResolveStaleBattle's offline/switch settlement passes false since that player has no
        // socket by construction.
        private async Task<StaleBattleResolution> AbandonBattle(Player player, PlayerState state, int? clientBattleMs = null, bool notify = true, CancellationToken cancellationToken = default)
        {
            // Idempotency backstop (#1874): this exact battle was already durably credited by an earlier
            // command whose session-state clear never reached the cache (e.g. a crash between the durable
            // credit and the now-awaited session save, docs/backend-persistence.md → Write-behind player
            // cache). The stale session is re-presenting an already-resolved battle on reconnect; crediting
            // it again would double-pay the same fight. The durable write already happened, so the session
            // only needs to catch up — clear it without replaying the credit. Checked first — needs only the
            // seed, not a replay — so this reconnect path stays O(1) instead of paying for the full
            // re-simulation below only to discard it (#1993).
            if (BattleAlreadyCredited(state, player))
            {
                state.ClearBattle();
                return new StaleBattleResolution(null, null);
            }

            var now = DateTime.UtcNow;
            // A stale battle can be far older than int.MaxValue ms (~24.9 days), so clamp before narrowing —
            // an unchecked out-of-range double-to-int cast is unspecified rather than a safe saturation.
            var wallClockMs = (int)Math.Clamp((now - state.BattleStartTime).TotalMilliseconds, 0, int.MaxValue);

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
            if (elapsedMs <= 0 || !TryResolveActiveBattle(state, out var enemy, out var result, out var playerMaterials, simulateMs))
            {
                state.ClearBattle();
                return new StaleBattleResolution(null, null);
            }

            if (result.Victory)
            {
                // The enemy died within the (wall-clock-capped) elapsed time the client simulated, so this
                // abandon resolved as a real victory. Pay it out exactly like EndBattleVictory (exp +
                // win/clear/challenge credit) rather than booking the win while silently withholding the
                // earned exp (#206).
                RecordVictory(player, enemy, result, state, now, notify, playerMaterials);
            }
            else if (!result.PlayerDied && wallClockMs < GameConstants.DefaultMaxBattleMs)
            {
                // Neither battler died and real server time since BattleStartTime is still under the cap:
                // this is not a stalemate, it is a battle genuinely still in progress (#1595). Gated on
                // wallClockMs (not the client-bounded elapsedMs used for simulateMs above) deliberately: a
                // client that under-reports clientBattleMs must not turn an already-expired battle (real
                // elapsed time past the cap) into a false "still in progress" with an ElapsedOffsetMs that
                // itself exceeds the cap it was just classified under. Leave it active (same enemy/seed/
                // snapshot, BattleStartTime unchanged) and hand it back with its elapsed offset instead of
                // booking a phantom draw. Only a battle whose real elapsed time reaches the cap is a genuine
                // draw, regardless of what the client claims to have simulated.
                return new StaleBattleResolution(
                    new BattleStartResult
                    {
                        Enemy = enemy,
                        Seed = state.BattleSeed ?? 0,
                        ElapsedOffsetMs = wallClockMs,
                        IsBossBattle = state.IsBossBattle,
                    },
                    null);
            }
            else
            {
                player.RecordBattleCompleted(enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId, now, state.BattleSeed, notify);
            }

            // Both outcome branches above (won or lost/drew) reach here — the still-in-progress branch
            // above returns early instead. Mirror EndBattleVictory/EndBattleLoss's pacing cooldown so an
            // outcome resolved via abandon can't skip it (#1851): without this, a client that never sends
            // DefeatEnemy and instead loops NewEnemy farms every kill's cooldown away.
            state.SetCooldown(now + PostBattleCooldown);
            state.ClearBattle();

            await _playerRepo.SavePlayer(player, cancellationToken);
            return new StaleBattleResolution(null, result.TotalMs);
        }

        // Books a victory: grants the earned exp and records the win (kills, zone clears, and the
        // challenge rewards driven off BattleCompletedEvent). Shared by the explicit victory path and
        // the won-abandon path so the two cannot drift — a battle that resolved as a win always pays
        // out the same way, regardless of how the battle was ended.
        //
        // Anti-cheat note: the two callers gate this payout differently and that asymmetry is intentional.
        // EndBattleVictory validates that enough *server-clock* wall time has elapsed since battle start for
        // the replayed battle to have actually finished (immune to client/server clock skew, since no
        // client-supplied timestamp is trusted) before paying out. The won-abandon path performs no such
        // elapsed-time check because it re-simulates capped at the *server-measured* elapsed wall-clock time
        // (AbandonBattle's elapsedMs) — a win only resolves if the enemy died within time the server itself
        // observed, so the server-measured cap is the (stronger) control there and nothing else is needed.
        // Both paths therefore require a server-validated timeline; neither can be claimed early.
        // internal (not private) so an integration test can assert the live PlayerRating snapshot directly:
        // EndBattleVictory returns only a client-facing DefeatResult, and the BattleStats this mutates is
        // carried on the BattleCompletedEvent, which the dispatcher clears after handling — leaving no other
        // seam to observe that result.Stats.PlayerRating is set from the snapshot rather than the live aggregate.
        internal DefeatRewards RecordVictory(
            Player player, CoreEnemy enemy, BattleResult result, PlayerState state, DateTime timestamp,
            bool notify = true, BattlerMaterials? playerMaterials = null)
        {
            // Rate the player for the reward from the same frozen snapshot the battle was simulated against, not
            // the live aggregate. Valid mid-battle socket commands (stat reallocation, gear swaps) can shift live
            // power between battle start and the victory claim — which would both diverge from the fought battle
            // and let a client deflate its power right before claiming to inflate the payout.
            //
            // playerMaterials lets a caller that already resolved it (TryResolveActiveBattle, moments earlier in
            // the same command) skip re-walking the item/mod/skill/proficiency/class resolution a second time
            // (#1897) — Build() below still constructs a fresh, single-use Battler regardless (a Battler mutates
            // its health and active effects during simulation, BattleSnapshot.GetBattlerMaterials), so the rating
            // still reads the pristine battle-start state, never the fought/mutated one. A caller with no
            // materials on hand (e.g. a direct unit-test call) falls back to resolving them here; RecordVictory
            // only runs after TryResolveActiveBattle has confirmed an active snapshot, so a null Snapshot on that
            // fallback path is a broken invariant rather than a reachable state.
            if (playerMaterials is null)
            {
                if (state.Snapshot is not { } snapshot)
                {
                    throw new InvalidOperationException("Cannot record a victory without an active battle snapshot.");
                }

                playerMaterials = snapshot.GetBattlerMaterials(
                    _items.GetItem, _itemMods.GetItemMod, _skills.TryGetSkill, _proficiencies.GetProficiency, ResolveClass);
            }

            var playerBattler = playerMaterials.Build();
            var rewards = new DefeatRewards(playerBattler, enemy);

            // Snapshot the player's rating onto the battle stats so the proficiency accrual normalizes activity
            // by the identical measure the reward curve uses (spike #1526 Decision 5) — captured here from the
            // same snapshot-built battler, not the live aggregate.
            result.Stats.PlayerRating = rewards.PlayerRating;

            player.GrantExp(rewards.ExpReward);
            // Thread both combat ratings onto the battle-completed event so the progress handler can normalize
            // each path's activity by max(playerRating, enemyRating) for the effect-based proficiency accrual
            // (spike #1526 Decision 5) — the same snapshot-measured ratings the exp reward above used.
            player.RecordBattleVictory(
                enemy, result, state.IsBossBattle, state.BattleZoneId ?? player.CurrentZoneId, timestamp,
                rewards.PlayerRating, rewards.EnemyRating, state.BattleSeed, notify);

            return rewards;
        }

        // Generates the simulation RNG seed from a cryptographic (non-time) entropy source. A wall-clock seed
        // (DateTime.Ticks) is monotonic and low-entropy in its low 32 bits — correlated and predictable between
        // battles — which makes it unsuitable as the shared starting point for the parity-identical crit/dodge
        // RNG (#178). The seed is server-generated and transmitted to the client as-is, so changing the source
        // does not affect how it is consumed. Shared by both start paths.
        internal static uint CreateBattleSeed() => BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));

        /// <summary>
        /// Rates the player's current live capability (<see cref="CombatRating.Rate"/>, spike #1526 Decision 7)
        /// for display — a numeric companion to the attributes/battle screens, distinct from the frozen
        /// snapshot rating <see cref="RecordVictory"/> pays rewards against. Reconstructs a fresh
        /// <see cref="BattleSnapshot"/> from the player's current state (rather than a stored battle snapshot),
        /// so the value reflects an allocation/gear/level change immediately instead of the last fought battle.
        /// </summary>
        public async Task<double> RatePlayer(Player player, CancellationToken cancellationToken = default)
        {
            var proficiencyLevels = await _progressRepo.GetProficiencies(player.Id, cancellationToken);
            return RatePlayer(player, proficiencyLevels);
        }

        /// <summary>
        /// Overload for callers that already loaded the player's proficiency levels for another purpose in the
        /// same command (e.g. <c>EquipItem</c>'s gear proficiency gate), so rating doesn't re-read the same
        /// Redis hash a second time.
        /// </summary>
        public double RatePlayer(Player player, IEnumerable<PlayerProficiency> proficiencyLevels)
        {
            var snapshot = BattleSnapshot.FromPlayer(player, ToProficiencyLevels(proficiencyLevels));
            var battler = snapshot.ToBattler(
                _items.GetItem, _itemMods.GetItemMod, _skills.TryGetSkill, _proficiencies.GetProficiency, ResolveClass);
            return CombatRating.Rate(battler, isPlayer: true);
        }

        // Captures the player's current proficiency levels for the battle snapshot, so the per-level/milestone
        // bonuses bake into the fight at its start (spike #982 area E). Proficiency progress lives on the
        // separate PlayerProgress aggregate, so it is read through the lean proficiency-only accessor rather
        // than the battle aggregate; the empty list (the universal state until proficiencies are authored and
        // opened) yields no proficiency modifiers, so the replay stays identical to today.
        private async Task<List<ProficiencyLevelSnapshot>> CaptureProficiencyLevels(int playerId, CancellationToken cancellationToken)
        {
            var proficiencies = await _progressRepo.GetProficiencies(playerId, cancellationToken);
            return ToProficiencyLevels(proficiencies);
        }

        // Projects proficiency progress to the battle-snapshot's level-only view. Duplicated (not shared) in
        // OfflineProgressService, which derives it from the progress aggregate it already loaded rather than
        // through this lean accessor — like ResolveClass below, a three-line, dependency-only projection not
        // worth a shared abstraction (CLAUDE.md).
        private static List<ProficiencyLevelSnapshot> ToProficiencyLevels(IEnumerable<PlayerProficiency> proficiencies) =>
            proficiencies
                .Select(p => new ProficiencyLevelSnapshot { ProficiencyId = p.ProficiencyId, Level = p.Level })
                .ToList();

        // Anti-cheat, server-clock only, shared by the victory and loss claim paths (#1630): a battle-end
        // claim cannot be accepted before enough real server time has elapsed since battle start for the
        // replayed outcome to have actually happened. Network latency only ever delays the claim, so a
        // legitimate claim always lands at or after the completion time; reject only when the server itself
        // observed measurably less elapsed time than the replay's duration (minus a one-tick slack for the
        // mid-tick battle-start alignment). Both ends use the server clock, so this is immune to client/server
        // clock skew — unlike a check against a client-supplied timestamp.
        private static bool ClaimedBeforeBattleCouldFinish(
            DateTime battleStartTime, int replayTotalMs, DateTime now, out DateTime battleCompletedAt)
        {
            battleCompletedAt = battleStartTime.AddMilliseconds(replayTotalMs);
            return now < battleCompletedAt - ElapsedBattleTimeTolerance;
        }

        // Idempotency backstop (#1874/#1993): true when this exact battle was already durably credited by an
        // earlier command whose session-state clear never reached the cache (e.g. a crash between the
        // durable credit and the now-awaited session save, docs/backend-persistence.md → Write-behind player
        // cache). Shared by all three battle-end paths — EndBattleVictory, EndBattleLoss, and AbandonBattle —
        // so a client re-presenting an already-credited battle can't double-pay it via whichever path it
        // calls directly, not just the reconnect-driven abandon path. Needs only the seed, so every caller
        // checks this before any replay work.
        private static bool BattleAlreadyCredited(PlayerState state, Player player) =>
            state.BattleSeed is uint seed && seed == player.LastCreditedBattleSeed;

        // Shared anti-cheat preamble for the three battle-end paths: guards that a battle is active, resolves
        // the snapshotted enemy, and replays the fight once. Returns false (with no outputs) when there is no
        // active battle, so each caller keeps only its outcome-specific branching. Centralising the guard, the
        // ?? 1 / ?? [] fallbacks, and the simulate call here keeps the replay surface from silently drifting.
        //
        // Also resolves the player's BattlerMaterials once and outputs it (#1897): the two victory paths
        // (EndBattleVictory, the won-abandon branch of AbandonBattle) can hand it straight to RecordVictory
        // instead of re-walking the same snapshot resolution a second time. A caller with no use for it (e.g.
        // EndBattleLoss) simply discards it.
        private bool TryResolveActiveBattle(
            PlayerState state,
            [MaybeNullWhen(false)] out CoreEnemy enemy,
            [MaybeNullWhen(false)] out BattleResult result,
            [MaybeNullWhen(false)] out BattlerMaterials playerMaterials,
            int? maxMs = null)
        {
            if (state.ActiveEnemyId is not int enemyId || state.Snapshot is null)
            {
                enemy = default;
                result = default;
                playerMaterials = default;
                return false;
            }

            var level = state.ActiveEnemyLevel ?? 1;
            var enemySkillIds = state.ActiveEnemySkillIds ?? [];

            enemy = _enemies.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} not found");

            playerMaterials = state.Snapshot.GetBattlerMaterials(
                _items.GetItem, _itemMods.GetItemMod, _skills.TryGetSkill, _proficiencies.GetProficiency, ResolveClass);

            // The seed is set alongside the snapshot at battle start (and cleared together), so a non-null
            // snapshot here guarantees a non-null seed — the ?? 0 mirrors the defensive fallbacks above.
            result = SimulateBattle(enemy, enemySkillIds, playerMaterials, state.BattleSeed ?? 0, maxMs);
            return true;
        }

        // Resolves the player's class for the locked-base distribution, failing loudly on an unresolvable id
        // (a content-data mistake) rather than silently dropping the attribute fingerprint — the player-load
        // missing-reference policy applied to the battle-assembly path.
        private CoreClass ResolveClass(int classId) =>
            _classes.GetClass(classId)
            ?? throw new InvalidOperationException($"Class {classId} could not be resolved from the catalogue.");

        private static BattleResult SimulateBattle(CoreEnemy enemy, IReadOnlyList<int> enemySkillIds, BattlerMaterials playerMaterials, uint seed, int? maxMs = null)
        {
            enemy.SetBattleSkills(enemySkillIds);

            var playerBattler = playerMaterials.Build();

            // The same seed shipped to the client at battle start, so the server's anti-cheat replay draws
            // the crit/dodge/block rolls from the identical RNG stream the client simulated.
            var simulator = new BattleSimulator(playerBattler, enemy.ToBattler(), seed);
            return simulator.Simulate(maxMs);
        }
    }

    public class BattleStartResult
    {
        public required CoreEnemy Enemy { get; set; }
        public required uint Seed { get; set; }

        /// <summary>
        /// Non-null when this battle was already in progress rather than freshly started (#1595): the real
        /// elapsed time (ms) since it began, which the client must fast-forward through — replay-to-offset,
        /// #1597 — before continuing live. Null for a freshly started battle.
        /// </summary>
        public int? ElapsedOffsetMs { get; set; }

        /// <summary>
        /// Whether this is (or, for a hand-back, was) a dedicated-boss fight rather than an idle-zone
        /// spawn — mirrors the authoritative <see cref="PlayerState.IsBossBattle"/> so a resumed hand-back
        /// (#1595/#1596) lets the client route into the boss loop instead of always defaulting to idle (#1647).
        /// </summary>
        public bool IsBossBattle { get; set; }
    }

    /// <summary>
    /// The outcome of <see cref="BattleService.ResolveStaleBattle"/>: either the stale battle is handed back
    /// still active (<see cref="Handoff"/> non-null, <see cref="SettledBattleMs"/> null), or it was resolved —
    /// crediting a win/loss/draw and clearing it (<see cref="Handoff"/> null) — in which case
    /// <see cref="SettledBattleMs"/> carries the credited battle's own duration, or stays null if there was no
    /// active battle to resolve in the first place.
    /// </summary>
    public record StaleBattleResolution(BattleStartResult? Handoff, int? SettledBattleMs);
}
