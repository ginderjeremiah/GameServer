# Spike #879 — Offline time tracking & rewards

- **Spike issue:** [#879](https://github.com/ginderjeremiah/GameServer/issues/879)
- **Status:** Research complete; direction decided with the project owner; split into implementation sub-issues [#1039–#1044](#implementation-issues). Not yet implemented.

## Goal

When a player returns after being away, credit them for the progress their idle loop *would* have made while disconnected. Track when the player was last active, and on the next login simulate the battles they missed (capped), then present a "welcome back" summary before they re-enter the game. The simulation must respect what the player's idle loop was actually doing at disconnect — idle-farming a zone **or** auto-challenging a zone's boss.

## The core enabling insight — offline battles are *stationary*

The whole feasibility argument rests on one fact established from the code: **a player's combat power and reward never change while they are offline.**

- A player's battle attributes come **only** from stat allocations + equipped items/mods (`BattleSnapshot.GetModifiers`, composed identically to the live `Player.GetAllModifiers`). Neither can change offline — allocating stat points and equipping gear are socket commands the absent player never sends.
- The player's **level is not used in combat math at all** — `Battler` stores `Level` but no combat path reads it; all math reads the attribute collection. Level only feeds the *threshold* for the next level-up.
- The exp reward is a function of **enemy power vs. the player's snapshot power** (`DefeatRewards`), and enemies scale with the **zone's** level range (`Zone.RollEncounterLevel`), independent of the player's level.

So every offline idle battle is an i.i.d. draw from the *same* distribution (random enemy, random level within the zone band, random enemy loadout, fresh RNG seed). The player accumulates exp, levels, and (unallocated) stat points, but none of that shifts the per-battle outcome or reward distribution. This is what makes a faithful full simulation both correct and bounded.

## Performance — full simulation is cheap, the cooldown is the throttle

The 5-second post-battle cooldown (`BattleService.PostBattleCooldown`) bounds the **battle count**: short fights are gated by the cooldown (≈ one battle / 5 s), long fights are few (the 2-minute `DefaultMaxBattleMs` cap). Per-battle sim cost scales with battle length (40 ms ticks). Working the `BattlePerformanceTests` numbers through the count/length trade-off, a full **10-hour** replay costs on the order of **~4–5 s of CPU worst-case** on a dev box, and realistically sub-second to ~1–2 s — matching the spike author's own "~3 s of sim per 5 h" estimate.

This sits comfortably under the **25 s per-command timeout** (`SocketHandler.DefaultCommandTimeout`) and the client's 30 s request backstop, so the simulation can run **inline within a single socket command** for V1.

## How it works today (what the feature must build on or fill)

- **Battle simulation is reusable as-is.** `BattleService.SimulateBattle` builds a player `Battler` from a snapshot + an enemy `Battler` and runs `BattleSimulator.Simulate(maxMs)`. `BattleFactory.CreateBattleEnemy` (random idle) and `CreateBossEnemy` (deterministic boss) already build battle-ready enemies from resolver funcs. The offline loop runs *fresh* battles (new random enemies, new seeds), not the active-battle anti-cheat replay.
- **Rewards are exp-only.** A victory grants exp (`DefeatRewards` → `Player.GrantExp`) plus statistics/challenge progress (`Player.RecordBattleCompleted` → `BattleCompletedEvent`). **There are no item drops on idle kills** — items/mods/skills are unlocked exclusively through *challenge* completions. So "offline rewards" = accumulated exp (→ levels → stat points) + whatever challenges complete (which unlock their item/mod/skill rewards).
- **No away-time anchor exists.** `Player.LastActivity` (a DB column) is set only when a player is created and read only for the admin player summary — **nothing updates it during play**. There is no other persisted "last seen" timestamp; `PlayerState` (Redis session cache) holds only `EnemyCooldown` / `BattleStartTime`.
- **The auto-challenge-boss state is frontend-only and ephemeral.** It is an in-memory `EnemyManager.autoFight` flag that **resets on loss/draw/stop and is never sent to the backend**. The backend cannot currently tell whether a disconnected player was idle-farming or boss-farming.
- **Challenge completions push one socket command each.** The live path emits a `ChallengeCompleted` server-push (over the Redis backplane) per completion. A batch of offline completions must not fan out into N pushes.
- **`GrantExp` clamps each grant to `MaxExpPerGrant` (100k).** This is a per-call anti-cheat clamp on the (client-triggered) live path.

## Decisions

Settled with the project owner during the spike:

1. **Full server-side simulation, not sample-and-extrapolate.** The away period is replayed battle-by-battle until the away-budget or the cap is exhausted. Chosen over sampling because the cap + cooldown make full sim cheap (above), it is *exact* for discrete outcomes (per-enemy kills, challenge thresholds) that sampling would only estimate, and it handles the boss "fight-until-loss" case naturally (a stopping process sampling cannot represent). The stationarity insight means there is no accuracy lost by *not* re-snapshotting the player as they level — their power is fixed for the whole window. (Sampling remains a possible future optimization if the inline cost ever bites; noted below.)

2. **Run inline within one socket command for V1, gated by a "welcome back" screen.** A `GetOfflineProgress` command computes the away time server-side, runs the whole simulation, applies the rewards, and returns a complete summary. The frontend shows a gate screen (spinner → summary) **before** starting the idle loop, so simulated and live battles never overlap. Async progress-streaming (the issue's "socket command → initial response → incremental progress pushes" idea) is deferred to a follow-up, to be picked up only if peak-login latency proves to be a problem.

3. **Respect the active idle-loop mode, including boss farming, in V1.** Both modes simply loop their battle type for the whole away budget, differing only in *which* enemy is fought; wins, losses, and draws all continue to the next battle.
   - **Idle mode** → loop random battles in the player's `CurrentZoneId` (matching `watchIdleStage`).
   - **Boss mode** (auto-challenge-boss on) → loop boss battles (fresh seed each, so crit/dodge/block RNG varies the outcome) in the boss's zone. Offline boss farming **keeps farming the boss through losses and draws** — it does not stop or fall back to idle. The online auto-fight-off-on-loss (`resolveBossLoss`/`resolveBossDraw` → `returnToIdle`) only fires while the player is *present* to observe the loss; with no observer offline, the loop just continues. That online transition therefore matters only in that it determines which mode is *persisted* at disconnect (decision 5), not the offline loop.

   Respecting this requires the backend to *know* the mode, which it does not today (decision 5).

4. **Away-time anchoring.** Maintain `Player.LastActivity` as a real "last active" timestamp: stamp it on battle completion (the issue's "last enemy defeated" anchor) so it is current at any disconnect, and reset it to "now" whenever offline progress is computed. Away time = `now − LastActivity`, computed **server-side** (never client-supplied). Offline rewards trigger only when away ≥ **5 minutes** and are simulated for at most **10 hours** (both per the issue). Resetting `LastActivity` on claim makes a re-claim (e.g. a reconnect) a no-op, so rewards cannot be double-collected.

5. **Persist the active idle-loop mode (idle vs. auto-challenge-boss) to the backend.** Because the mode is needed at *next login* regardless of how long the player was away, it is persisted on the durable `Player` aggregate (symmetric with `CurrentZoneId`, which already persists the idle location), carrying the boss zone when in boss mode. The frontend syncs it via a lightweight socket command when the auto-fight toggle changes and when the loop returns to idle; the backend also resets it to idle when it records a boss loss/draw, as a consistency backstop.

6. **Apply offline exp per simulated victory via the existing `Player.GrantExp`.** Each battle's exp is small, so the per-grant `MaxExpPerGrant` clamp never truncates a legitimate offline haul, the level-up loop runs correctly as exp accumulates, and the per-event envelopes buffer into the single write-behind batch flushed on save. `PlayerLeveledUpEvent` is in-process only with no active consumer, so the level-up burst is harmless — the net level gain is consolidated into the summary, not surfaced per level.

7. **Consolidate statistics and challenges into one pass; suppress per-challenge pushes.** The offline path accumulates statistic deltas across all simulated battles, applies them, evaluates the affected challenges **once** at the end, unlocks their rewards, and surfaces the completions in the welcome-back summary. The live per-challenge `ChallengeCompleted` push is suppressed for the offline window — the summary is the notification, and the client re-syncs its authoritative state (progress, inventory, skills) when leaving the gate. This is equivalent to per-battle evaluation because an offline challenge unlock cannot change the loadout, so it never affects a later offline battle. The per-battle statistic-update logic should be factored so the live `BattleCompletedEvent` handler and the offline batch share it (DRY) rather than duplicating it.

## Flow (end to end)

1. Player logs in (HTTP) → socket connects → loading screen fetches reference data.
2. Frontend issues `GetOfflineProgress` **before** starting the idle loop.
3. Backend: away = `now − LastActivity`. If `< 5 min`, set `LastActivity = now` and return an empty result. Otherwise resolve any stale in-flight battle, read the persisted loop mode + zone, and run the capped simulation (decisions 1, 3).
4. Backend applies rewards (exp per victory, consolidated stats + challenges + unlocks), sets `LastActivity = now`, persists once, and returns the summary.
5. Frontend: if the summary has content, show the welcome-back gate (away duration, idle/boss mode, battles won/lost/drawn, total exp, levels & stat points gained, challenges completed and what they unlocked), then re-sync state and enter the game. If empty, enter the game directly.

## Implementation issues

Created as sub-issues of #879. Dependency order: **#1039**, **#1040**, **#1041** are independent; **#1042** depends on all three; **#1043** depends on #1042.

- **[#1039](https://github.com/ginderjeremiah/GameServer/issues/1039) — Away-time tracking foundation** *(enhancement, claude, scope: small)*. Maintain `Player.LastActivity` as a live "last active" timestamp on the core aggregate (stamp on battle completion; reset to now on offline claim/login) and persist it through the existing write-behind path. No user-facing effect on its own; the anchor every other piece reads.
- **[#1040](https://github.com/ginderjeremiah/GameServer/issues/1040) — Persist the active idle-loop mode (idle vs. auto-challenge-boss)** *(enhancement, claude, scope: medium)*. Persist mode + boss zone on the `Player` aggregate; a new socket command to sync it from the frontend `AutoFightToggle` / `returnToIdle`; backend reset-to-idle on a recorded boss loss/draw. Closes the "the backend can't tell what loop was active" gap.
- **[#1041](https://github.com/ginderjeremiah/GameServer/issues/1041) — Core offline simulation engine (`Game.Core`)** *(enhancement, claude, scope: large)*. A pure domain simulator that, given the player snapshot, mode, zone, away budget, cap, cooldown, and resolver funcs, loops the mode's battle type for the whole budget (decision 3), accounting away-budget as `battleDuration + cooldown` per battle up to the 10 h cap, and accumulating per-battle outcomes/exp/kill-counts/stat deltas. Reuses `BattleFactory`/`BattleSimulator`/`DefeatRewards`; seed source injected for deterministic tests. Heavily unit-tested (idle through the cap, boss through the cap with mixed win/loss/draw, sub-threshold, all-draw zone, cap-vs-budget boundary).
- **[#1042](https://github.com/ginderjeremiah/GameServer/issues/1042) — Offline reward application, challenge consolidation, orchestration & socket command** *(enhancement, claude, scope: large)*. Application-layer orchestration: compute away-time (#1039), read mode/zone (#1040), resolve any stale active battle, call the core simulator (#1041), apply exp per victory via `GrantExp` (decision 6), apply consolidated statistics + evaluate challenges once + unlock rewards with the live push suppressed (decision 7), set `LastActivity = now`, persist once. Plus the `GetOfflineProgress` socket command, the summary DTO, and codegen. Depends on #1039 + #1040 + #1041.
- **[#1043](https://github.com/ginderjeremiah/GameServer/issues/1043) — Frontend welcome-back gate & summary** *(enhancement, claude, scope: medium)*. A gate between the loading screen and the game shell that calls `GetOfflineProgress`, renders the summary (away duration, idle/boss mode, battles won/lost/drawn, exp/levels/stat-points, challenges + unlocks), re-syncs authoritative state, and enters the game — a no-op pass-through when there are no rewards. Includes the frontend half of #1040's mode-sync if not already shipped there. Depends on #1042.

Deferred follow-up:

- **[#1044](https://github.com/ginderjeremiah/GameServer/issues/1044) — Async simulation progress streaming** *(enhancement, scope: large; deferred, not `claude`-tagged)*. Replace the inline run with a background simulation that streams incremental progress over the Redis backplane (the issue's original idea), if peak-login latency or thread-pool pressure makes the inline approach a problem. Gated on a real measured need and its own design pass (background-task lifecycle, off-loop player-state synchronization against the per-socket command lock, partial-result persistence, disconnect-mid-sim). Depends on #1042.

## Notes / follow-ups surfaced

- **Battle parity does not apply to the offline sim.** The client never replays these battles — the backend is the sole, authoritative simulator — so there is no parity surface to mirror. It reuses the shared `BattleSimulator`, so results stay consistent with online battles by construction.
- **CPU-waste guard.** A run where every fight is a 2-minute draw (an over-matched idle zone, or a boss the player can neither beat nor lose to) earns nothing but costs the most to simulate, and could be triggered repeatedly by reconnecting. The 10 h cap bounds it; #1042 should additionally short-circuit a run that produces no progress over its first batch of battles. The inline loop must also observe the command cancellation token so it unwinds rather than risking the 25 s timeout.
- **Balance.** Offline boss farming continues through losses and draws (decision 3), so leaving auto-fight on against a winnable, over-leveled boss farms it for the whole budget — lucrative (the exp multiplier runs up to the 4× cap) and it racks up `BossesDefeated`; against a boss only won sometimes, the losses burn budget for no reward while the wins still accrue. This is a deliberate divergence from the present-player loop (which drops to idle on a loss) and a balance lever to watch during tuning, not a code change.
- **`LastActivity` precision.** Anchoring on battle completion slightly over-counts away time for a session that connected but never battled (the player earned nothing then anyway, and the cap bounds it). Stamping on clean disconnect/logout too would tighten it — a minor refinement, not required for V1.
- **Documentation to update on landing:** `docs/game-design.md` (a new "Offline rewards" section), `docs/backend.md` / `docs/backend-battle.md` (the offline simulation path and its mode persistence), and `docs/frontend-screens.md` (the welcome-back gate screen).
