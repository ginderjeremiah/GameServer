# Spike #1073 — Recover idle time lost to background-tab throttling

- **Spike issue:** [#1073](https://github.com/ginderjeremiah/GameServer/issues/1073)
- **Status:** Direction settled with the project owner (2026-07-04); implementation issues filed (see table below). Supersedes the earlier write-up in PR [#1584](https://github.com/ginderjeremiah/GameServer/pull/1584) (closed unmerged) — the owner review replaced its client-side mechanism (clamp-driven fast-forward) with a Web Worker tick source; the server-side design carried over unchanged.
- **Related:** surfaced by [#922](./922-multiple-players-per-account.md) (Decision 6); its sibling mitigation [#1074](https://github.com/ginderjeremiah/GameServer/issues/1074) (allowlist nudge) already shipped; reuses the [#879](./879-offline-rewards.md) offline simulator (#1042).

## The problem, and what's changed since the issue was filed

The idle loop is client-driven: `LogicalEngine.logicLoop` (`UI/src/lib/engine/logical-engine.ts:53-58`) polls every 10ms via `window.setInterval` and computes `timeDelta` from `performance.now()`. `update()` (`:60-75`) clamps any single poll to at most `tickSizeX5` (200ms) of *simulated* battle progress — the excess is added to the engine's wall-clock but never fed into a battle tick. Browsers throttle hidden-tab page timers (≥1s, ~once/minute after sustained hiding), so a backgrounded character's simulated battle drifts behind real time indefinitely.

Two claims in the original issue needed correcting during research:

- **"No `visibilitychange` handling today" is stale.** #1074 shipped since: `BackgroundThrottleMonitor` (`UI/src/lib/engine/background-throttle-notice.ts`) listens for `visibilitychange` and accumulates lost time via the `onIdleTimeLost` hook (emitted by the clamp, `logical-engine.ts:65`) — but purely to show a one-time allowlist-nudge toast. It never touches the engine or the server.
- **#1042 (offline rewards) already implements most of the issue's proposed mechanism.** `OfflineProgressService.SimulateProgress` (`Game.Application/Services/OfflineProgressService.cs:100-160`) resolves the stale in-flight battle first (replay from `BattleSeed`/`Snapshot`, capped at `DefaultMaxBattleMs` = 2 min) and then runs the whole-battle catch-up loop. The two genuine gaps are at its edges: a non-concluding replay is booked as a draw and discarded rather than handed back as "still in progress" (`BattleService.AbandonBattle`, `Game.Application/Services/BattleService.cs:338-390`), and the whole-battle loop (`Game.Core/Battle/Offline/OfflineProgressSimulator.cs:28-85`) drops its trailing remainder instead of carrying it into the next battle's starting offset.

## Decision 1 — a Web Worker tick source is the primary client-side fix (#1594)

Replace `LogicalEngine`'s `window.setInterval` with a dedicated module worker that posts a message every polling interval; the main thread drives `logicLoop` from `onmessage`. Dedicated-worker timers are not throttled in Chromium/Firefox, and `message` delivery to a hidden page is not throttled either — so the engine keeps ticking at full rate while hidden and the clamp stops firing in the common case. This changes the common case from "catch up on return" to "never fall behind": hidden-tab completions, challenge pushes, and proficiency XP report through the existing flow in real time, and the hidden-but-connected dead zone (socket alive, so `LastActivity` stays fresh and #1042 never triggers) closes entirely. It is the programmatic equivalent of the browser-settings allowlisting the #1074 toast asks users for.

What a worker **cannot** cover — and why the server-side work below is needed regardless:

- **Page freeze / tab discard** (Chrome Memory Saver, battery saver) stops the whole page *including its workers* — and Memory Saver is exactly the audience #1074's notice targets.
- **Safari** suspends hidden tabs wholesale (fully on iOS), workers included.
- **Sleep, browser close, hard refresh, socket reconnect** — client state is simply gone.

**Rejected: moving the battle engine itself into the worker.** `BattleEngine` reads statified Svelte `$state` managers (`inventoryManager`, `playerManager`, `playerProficiencies`, `staticData`) and emits `logMessage`/`notifyCombatFloat` per tick; the `Battler` objects are rendered directly by components. Off-threading that means serializing the build in, streaming events out, and a thread hop between the sim and the socket — a large refactor that buys nothing, because the worker dies with the page in every case above anyway.

Two consequences, handled as their own issues:

- **`BackgroundThrottleMonitor` self-gates and needs no change**: where the worker trick works, `onIdleTimeLost` stops firing while hidden, so the toast naturally only appears where it still helps. The clamp and `onIdleTimeLost` stay as the safety valve and instrumentation.
- **Presentational side effects now run while hidden** (#1598): floater-removal `setTimeout`s are throttled to ~1/min batches while battles keep resolving; floats should simply not spawn while `document.hidden` (they are `aria-hidden` presentational). The combat log is already capped.

## Decision 2 — server-authoritative resume via backdated `BattleStartTime` (#1595, #1596)

When client state is gone (freeze/discard, reconnect, refresh), the fix extends #1042's two lossy edges rather than adding a parallel mechanism:

- **Hand back a still-in-progress stale battle (#1595).** In `AbandonBattle`'s non-concluding branch, when `now - BattleStartTime` is still under the 2-minute cap, keep the battle active (same enemy/seed/snapshot, `BattleStartTime` unchanged) and surface it to the client with its elapsed offset instead of booking a draw. Only a replay that reaches the cap with both battlers alive is a genuine draw.
- **Carry the offline loop's trailing remainder (#1596).** After the whole-battle crediting loop, set up the next battle backdated by the remainder via the existing `PlayerState.SetActiveBattle(..., startTime:)` overload (`Game.Core/Entities/PlayerState.cs:55`), serving the post-battle cooldown out of the remainder first. This closes the boundary-slice loss #879 accepted as a known limitation.

Because every elapsed-time check in the codebase already derives from `now - state.BattleStartTime` (the victory tolerance check at `BattleService.cs:268-286`, the cooldown gate, the client's own battle payload) and the server itself sets `BattleStartTime`, **"already in progress" falls out of the existing anti-cheat model for free** — no new fields, no new validation surface. The whole-battle crediting loop itself is untouched (it is a trusted internal server loop with no per-battle rate limit to trip).

## Decision 3 — client replay-to-offset, scoped to server-supplied offsets only (#1597)

A battle handed back mid-flight must be reconstructed client-side: reset from the enemy instance/seed as today (`UI/src/lib/engine/battle/battle-engine.ts:194-207`), then run the shared `battleStep` headless up to the offset with the presentational side effects `logicalUpdate` emits suppressed (`battle-engine.ts:323-383`), then continue live. The offset is bounded by the 2-minute cap (≤ ~3000 ticks), so cost is trivial.

This makes the resume path a **third consumer of `battleStep`**, beyond the closed live-engine/parity-simulator pair `docs/frontend.md` documents. Accepted (owner sign-off): it is unavoidable in any design that resumes a battle at a non-zero offset, and it reuses the parity-tested stepper rather than adding game math. Update `docs/frontend.md`'s battle-parity section when #1597 lands.

**Dropped: client-measured-gap fast-forward** (the superseded write-up's §1 — a second `onIdleTimeLost` consumer that catches up the in-memory battler). The worker covers throttling, and a freeze-thaw short enough that the socket survives is an accepted, small loss that funnels into the reconnect path (the thawed client just finishes its battle late; the server's tolerance check only rejects *early* completions). If it proves meaningful, wiring `onIdleTimeLost` to #1597's primitive is a cheap follow-up — the hard part will already exist.

## What this explicitly does not need

- **No mid-battle state serialization** — both paths only backdate `BattleStartTime` and replay from the existing `Snapshot`/`BattleSeed`.
- **No new anti-cheat surface** — both paths route through checks that already derive everything from `BattleStartTime`.
- **No changes to #1042's whole-battle loop internals** — only its two edges.

## Implementation issues

| Issue | Scope | Summary |
| --- | --- | --- |
| [#1594](https://github.com/ginderjeremiah/GameServer/issues/1594) | small | Worker tick source for `LogicalEngine`; clamp + `onIdleTimeLost` remain as safety valve/instrumentation |
| [#1595](https://github.com/ginderjeremiah/GameServer/issues/1595) | medium | `AbandonBattle` hands back a still-in-progress stale battle (and defines the battle+offset payload contract) |
| [#1596](https://github.com/ginderjeremiah/GameServer/issues/1596) | medium | Offline loop's trailing remainder becomes a backdated next active battle (depends on #1595's contract) |
| [#1597](https://github.com/ginderjeremiah/GameServer/issues/1597) | medium | `BattleEngine` replay-to-offset (third `battleStep` consumer; consumes #1595/#1596's payload) |
| [#1598](https://github.com/ginderjeremiah/GameServer/issues/1598) | small | Hidden-tab presentational side-effect hygiene (follow-up to #1594) |

## Documentation to update on landing

- **`docs/frontend.md`** — battle-parity section, once `battleStep` gets its third caller (#1597).
- **`docs/backend-battle.md`** — offline-rewards section (a stale in-flight battle is no longer unconditionally settled/discarded; it may carry forward as a backdated active battle).

## Out of scope / deferred

- **Server-side authoritative idle simulation** (genuine parallel progression) — a much larger architectural shift, out of scope per #922's own framing.
- **Client-measured-gap fast-forward** — dropped above; revisit only if the freeze-thaw-with-live-socket loss proves meaningful in practice.
