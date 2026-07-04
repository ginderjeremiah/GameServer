# Spike #1073 — Recover idle time lost to background-tab throttling

- **Spike issue:** [#1073](https://github.com/ginderjeremiah/GameServer/issues/1073)
- **Status:** Research complete; a recommended direction follows below, but **this has not been reviewed with the project owner** — the usual spike collaboration step (research → owner sign-off → planning) hasn't happened synchronously for this pass. Filed as a recommendation, not a settled decision. No implementation issues have been created yet; a comment on #1073 flags the open questions.
- **Related:** surfaced by [#922](./922-multiple-players-per-account.md) (Decision 6); its sibling mitigation [#1074](https://github.com/ginderjeremiah/GameServer/issues/1074) (allowlist nudge) already shipped; reuses the [#879](./879-offline-rewards.md) offline simulator (#1042).

## The problem, and what's changed since it was filed

The idle loop is client-driven: `LogicalEngine.logicLoop` (`UI/src/lib/engine/logical-engine.ts:53-58`) polls every 10ms via `setInterval` and computes `timeDelta` from `performance.now()`. `update()` (`:60-75`) clamps any single poll to at most `tickSizeX5` (200ms) of *simulated* battle progress — the excess (`lostTime`) is added straight to `this.time` (the engine's wall-clock, line 63) but is **never fed into a battle tick**, so the actual `Battler` state silently falls behind wall-clock time. Browsers throttle hidden-tab timers (≥1s, ~once/minute after sustained hiding), so a backgrounded character's simulated battle state drifts further behind real time the longer the tab stays hidden — permanently, since nothing today catches it up.

**One claim in the original issue is now stale:** "there is no `visibilitychange` handling today." Issue #1074 shipped since: `BackgroundThrottleMonitor` (`UI/src/lib/engine/background-throttle-notice.ts`) listens for `visibilitychange`, accumulates lost time via the `onIdleTimeLost` hook (already emitted by the clamp above, line 65 of `logical-engine.ts`), and shows a one-time dismissible toast past a 60s threshold nudging the user to allowlist the tab. **It is purely observational** — it never touches `this.time`, the battle engine, or the server. The core problem (lost simulation, not lost awareness) is exactly as open as the issue described.

**A second claim needs sharpening, not correcting:** the issue frames #1042 (offline rewards) as discarding an in-flight battle and starting fresh. Reading `OfflineProgressService.SimulateProgress` (`Game.Application/Services/OfflineProgressService.cs:100-160`) and `BattleService.ResolveStaleBattle`/`AbandonBattle` (`Game.Application/Services/BattleService.cs:338-390`) shows #1042 already implements two of this spike's three mechanism steps, in the right order: it resolves the stale in-flight battle first (replay from `BattleSeed`/`Snapshot` to the elapsed wall-clock, capped at `DefaultMaxBattleMs` = 2 min), *then* runs the whole-battle catch-up loop for the remaining away window. What it does **not** do:
- If the in-flight replay doesn't conclude within the 2-minute cap (`BattleSimulator.Simulate` falls through to a non-victory result — `Game.Core/Battle/BattleSimulator.cs:37-84`), it's booked as a drawn battle with no rewards and discarded. There's no "still in progress, hand it back with an offset" branch — genuinely missing, exactly as the issue anticipated.
- The whole-battle loop (`OfflineProgressSimulator.Simulate`, `Game.Core/Battle/Offline/OfflineProgressSimulator.cs:28-85`) always starts each battle at `timeElapsed = 0` and never carries a trailing remainder forward as the next active battle's starting offset — when the player returns, the live loop starts a fresh battle from scratch. This is the one piece of the mechanism with no existing analogue anywhere in the codebase.

## Recommended design

Two genuinely different situations need two different fixes; conflating them was the main risk in the original framing.

### 1. Throttled-but-connected tab (the common case) — client-local silent fast-forward

While the tab is merely throttled (hidden, but the socket is alive — this is *not* an away/offline window; `LastActivity` never falls behind enough to trigger #1042), the loss is purely client-side: `this.time` already tracks true elapsed wall-clock (`logical-engine.ts:63`), but the battle's simulated `timeElapsed` doesn't. The fix doesn't need a new detection mechanism — **`onIdleTimeLost` already fires with the exact lost-ms figure, every time the clamp triggers** (`logical-engine.ts:65`); today only the #1074 toast consumes it. Recommend giving `BattleEngine` a second consumer of the same hook that, given `lostMs`, runs the existing pure `battleStep` (`UI/src/lib/battle/battle-step.ts`, already the sole shared engine/parity stepper) in a tight loop up to `lostMs` **without** the surrounding side effects `logicalUpdate` normally emits (`logMessage`, `notifyCombatFloat`, effect-application logging, DoT/HoT flush — `battle-engine.ts:323-383`), then resumes normal per-tick logging from the caught-up state. Concretely: a `fastForward(ms)` mode on `BattleEngine` that runs the same stalemate/victory/defeat checks `logicalUpdate` does (so a battle that would have concluded during the gap resolves correctly) but suppresses only the presentational side effects.

This is **not new game math** — `battleStep` is already the single parity-tested source of truth both `BattleEngine.logicalUpdate` (live) and `BattleSimulator` (headless, parity-test-only today per `docs/frontend.md:27-39`) drive. The risk is confined to wiring a new caller onto proven code, not to inventing new simulation logic. It *is*, however, a genuinely new **consumer** of that shared stepper outside test code — worth flagging in `docs/frontend.md`'s battle-parity section once it lands, since "what drives `battleStep`" is exactly the kind of fact that section exists to keep current.

**Open question left to the owner:** should silent fast-forward run for *any* clamp event (even a single 200ms-over poll), or only past some accumulated threshold (mirroring #1074's 60s notice threshold, so tiny stutters don't pay a fast-forward-loop cost every poll)? Recommend accumulating like `BackgroundThrottleMonitor` already does and fast-forwarding in batches rather than per-poll, but the actual threshold is a tuning call, not an architecture one.

### 2. Suspended/discarded tab, or genuine reconnect — server-authoritative resume

When the client state itself is gone (page discarded by the browser, hard refresh, or a socket reconnect after a long gap), there's nothing left client-side to fast-forward — this is exactly #1042's existing territory, and the fix is an extension to it, not a parallel mechanism:

- **Fix the "still in progress" branch.** In `AbandonBattle`, when the capped replay doesn't conclude (today: booked as a draw, battle cleared), instead compute the new active battle's start time as `now - remainderMs` and call the existing `PlayerState.SetActiveBattle(..., startTime: ...)` with the **same** enemy/seed/snapshot the abandoned battle already had, rather than clearing it. Because every existing elapsed-time computation in the codebase derives from `now - state.BattleStartTime` (the anti-cheat check in `EndBattleVictory`, the cooldown gate, the client's own battle-start payload), **backdating `BattleStartTime` is enough to make the battle "already in progress" fall out of the existing model for free** — no new fields, no new validation path. The client receives a battle that started in the past and fast-forwards to catch up using the *same* silent-fast-forward primitive from §1, this time driven by a server-supplied target offset instead of a client-measured gap.
- **Carry the trailing remainder of the whole-battle catch-up loop the same way.** Once `OfflineProgressSimulator`'s budget is exhausted mid-cooldown-or-mid-battle for its last iteration, hand the leftover time to the same "start the next battle backdated by the remainder" step instead of leaving the active-battle slot empty for the live loop to start fresh at zero. This closes the exact boundary-slice loss #879 accepted as a known limitation (`docs/spikes/879-offline-rewards.md`).
- **This reuses #1042's simulator verbatim** for the whole-battle-crediting middle step — no changes needed there. The anti-cheat "bursts of credited battles" concern the original issue raised turns out to be **already answered**: that loop never goes through `NewEnemy`/`EndBattleVictory`/the cooldown gate at all (`OfflineProgressSimulator.Simulate` is a trusted internal server loop with no socket round-trips), so there's no existing per-battle rate limit for it to trip. The only place backdating touches anti-cheat is the trailing-remainder battle, and since the server itself sets `BattleStartTime` in the past (never client-supplied), the existing `battleCompletedAt = BattleStartTime + totalMs` tolerance check (`BattleService.cs:268-286`) and the `NewEnemy` cooldown gate both keep working unmodified — a victory reported soon after resuming naturally already satisfies "enough wall-clock time has passed," because the server backdated the clock itself.

### What this explicitly does not need

- **No mid-battle state serialization**, confirming the issue's key insight — everything above only ever backdates `BattleStartTime` and replays from the existing `Snapshot`/`BattleSeed`.
- **No new anti-cheat surface.** Both paths route through checks that already exist and already assume nothing about when `BattleStartTime` itself was set.
- **No changes to #1042's whole-battle loop's internals** — only what happens at its two edges (the stale in-flight battle before it runs, and the trailing remainder after it stops).

### The one real trade-off worth an explicit owner call

§1's client-local fast-forward is a **new consumer of `battleStep` outside the parity-test suite** — today `docs/frontend.md` frames "who drives `battleStep`" as a closed, two-member list (the live engine, the headless parity simulator). Adding a third caller is low-risk (same proven stepper) but is a real change to that architectural fact, not just an implementation detail, so it belongs in that doc once this lands. Flagging rather than deciding unilaterally, since `docs/frontend.md`'s battle-parity section is exactly the kind of "sets expectations on fundamental architecture" content CLAUDE.md asks to keep deliberate about.

## Proposed implementation breakdown (not yet filed)

Held back pending owner review of the direction above, per CLAUDE.md's spike-collaboration expectation — filing these now would be starting the "planning" step before that happens.

| Area | Scope (est.) | Summary |
| --- | --- | --- |
| Client silent fast-forward | medium | `BattleEngine.fastForward(ms)` reusing `battleStep`; wire a second `onIdleTimeLost` consumer with an accumulate-then-catch-up threshold |
| Server resume: stale-battle remainder | medium | `AbandonBattle`'s non-concluding branch backdates a new active battle via `SetActiveBattle` instead of drawing it |
| Server resume: offline-loop remainder | small–medium | `OfflineProgressSimulator`/`OfflineProgressService` hand their trailing remainder to the same backdating step |
| Docs | small | `docs/frontend.md` battle-parity section (new `battleStep` consumer); `docs/backend-battle.md` offline-rewards section (stale battle no longer always discarded) |

## Documentation to update on landing

- **`docs/frontend.md`** — battle-parity section, once `battleStep` gets a third caller.
- **`docs/backend-battle.md`** — "Offline rewards simulation" section (the stale in-flight battle is no longer unconditionally settled/discarded; it may now carry forward as a backdated active battle).

## Out of scope / deferred

- **Server-side authoritative idle simulation** (genuine parallel progression) — a much larger architectural shift, out of scope per #922's own framing.
- **Tuning the fast-forward batching threshold** — a numbers call for whoever implements §1, not an architecture decision.
