# Spike #96 — Dedicated per-zone boss + "Challenge Boss" battle action

- **Spike issue:** [#96](https://github.com/ginderjeremiah/GameServer/issues/96)
- **Status:** Design decided; split into implementation sub-issues #186–#189 (+ deferred follow-up #190). Not yet implemented.
- **Design input:** Claude Design handoff bundle (`svelte-game`, chat8) — the `fight-boss.jsx` mockup is the target UI.

## Goal

Replace the current incidental boss mechanic — a boss is any `Enemy.IsBoss` that randomly rolls out of a zone's weighted spawn table — with a deliberate design: each zone has **exactly one dedicated boss**, fought via an always-available **"Challenge Boss"** action, and defeating that boss is what "clears" the zone. This also refines the `ZonesCleared` statistic from #89's stop-gap to its intended meaning.

## Current state (where the coupling lives)

- **Boss = `Enemy.IsBoss` in the random pool.** Bosses appear via the zone's weighted `ZoneEnemy` table; there is no dedicated boss, no challenge action, no determinism.
- **Battle start:** `NewEnemy` socket command → `BattleService.StartBattle` → `BattleFactory.CreateBattleEnemy` rolls a level in `[LevelMin, LevelMax]` and resolves a *random* weighted enemy via `Enemies.GetRandomDomainEnemy`. Resolve path is `DefeatEnemy`/`BattleLost` → `EndBattleVictory`/`EndBattleLoss` → `SimulateBattle` (validation) → `BattleCompletedEvent`.
- **Clear tracking (#89 stop-gap):** `PlayerProgress.RecordBattleCompleted` increments `BossesDefeated` (global) and `ZonesCleared` (global + per-zone keyed by `CurrentZoneId`) on **any** `IsBoss` victory. The global `ZonesCleared` therefore equals `BossesDefeated` and counts boss-victory *events*, not distinct zones.
- **Zone navigation is ungated:** `ZoneNav` orders zones by `.order` and lets the player walk to any of them; there is no locking/progression system.
- **Frontend:** `EnemyManager` loops `NewEnemy` continuously (idle farming); the battle-engine simulates and reports victory/defeat. `IEnemy.isBoss` exists on the contract but no frontend combat logic keys off it.

## Decisions

Settled with the project owner during the spike:

1. **Dedicated boss is a first-class `Zone` field, not a spawn-table flag.** Add `BossEnemyId` (nullable FK → `Enemy`) on `Zone`, distinct from `ZoneEnemy`. `Enemy.IsBoss` is **repurposed** to mean "is a dedicated zone boss" (kept for the admin boss-picker filter and FK validation), not deleted.
2. **Challenge-only.** The dedicated boss is **never** in the random idle spawn table; it is fought solely via the Challenge action. Normal idle enemies are never bosses.
3. **Dedicated boss level.** Add `BossLevel` on `Zone`, independent of `[LevelMin, LevelMax]` (the mockup boss is LV 18 vs LV 8–11 normals). Used **deterministically** — no random roll.
4. **Deterministic encounter.** A new `ChallengeBoss` command → `BattleService.StartBossBattle` → `BattleFactory.CreateBossEnemy` builds the boss at the fixed level with its **full authored skill loadout** (no random skill selection). This is more deterministic than the normal path, which is exactly what frontend/backend battle parity needs. The `seed` stays reserved for in-sim RNG.
5. **Mark the battle as a boss challenge.** Persist an `IsBossBattle` marker (+ zone id) on `PlayerState` at start and thread it through `BattleCompletedEvent`, so the resolve path knows the victory was a *dedicated-boss* clear rather than inferring from `enemy.IsBoss` + `CurrentZoneId`.
6. **Refined clear semantics.**
   - Global `ZonesCleared` = **distinct zones ever cleared** (increment only on a zone's first clear, the per-zone 0→1 transition).
   - Per-zone `ZonesCleared` = **binary 1** once cleared (idempotent on re-clears).
   - Farm counts live in `BossesDefeated` (global + per-enemy), incremented on every dedicated-boss victory.
7. **Frontend behaviour.** Built on the **Mirror** layout. Boss is always challengeable. **Auto-fight** only re-challenges the boss; with it off, a victory returns to the normal idle loop; the two loops never run at once. On loss: record it, turn auto-fight off, return to the boss-available state. Gold boss accent (`#e8c878`) is a **theme token** in `+layout.svelte`, not hardcoded.
8. **Zone-navigation locking is deferred** (#190). The victory overlay shows "cleared" but does not lock/unlock navigation in the initial feature — locking is a gameplay/UX change that warrants its own design pass (new-player flow, existing-player grandfathering). It is cheap to add later because "cleared" is derivable from the per-zone `ZonesCleared` statistic + zone order.

## No cooldown / loss behaviour

- The boss challenge has **no cooldown** (it's "always available"); the normal idle loop keeps its existing cooldown.
- A boss **loss** records `BattlesLost`, performs no clear, and returns to the boss-available state.

## Implementation issues

Tracked as sub-issues of #96:

1. **#186 — Data model + admin authoring** *(foundation)*: `BossEnemyId` + `BossLevel` on `Zone`, EF migration, `Zone` contract + codegen, Workbench boss field, repurposed `IsBoss` + FK validation.
2. **#187 — Backend "Challenge Boss" battle action** *(depends on #186)*: `ChallengeBoss` command, `StartBossBattle`, `CreateBossEnemy` (deterministic), `PlayerState` boss marker threaded through `BattleCompletedEvent`.
3. **#188 — Refine `ZonesCleared` / `BossesDefeated` semantics** *(depends on #187)*: distinct-zones global counter, binary per-zone clear, farm counts on `BossesDefeated`; updates `docs/game-design.md`.
4. **#189 — Frontend Challenge Boss UI + engine wiring** *(depends on #186 + #187)*: Svelte 5 port of the mockup, boss accent token, `EnemyManager` boss mode (auto-fight / retreat / loss), Cleared seal from per-zone statistics; documents the screen in `docs/frontend-screens.md`.

Deferred follow-up:

- **#190 — Zone progression locking** *(deferred, not `claude`-tagged)*: gate `ZoneNav` on a cleared predecessor once the boss feature lands.

#186 is the foundation; #187 and #189 depend on it; #188 depends on #187; #189 also depends on #187.

## Notes / follow-ups surfaced

- **Battle parity:** the boss is deterministic, so parity is straightforward — but the boss battle-setup scenario must be **mirrored in both the frontend and backend test suites** (the project's standing rule for battle logic).
- **Seed data / e2e:** existing seed and `e2e-seed.sql` zones have no boss assigned; assigning bosses to seeded zones (so the fight screen is exercised end-to-end) may be a small follow-up rather than part of #186.
- **Documentation to update on landing:** `docs/game-design.md` (Zone Clears), `docs/backend.md` (Battle setup), `docs/frontend-screens.md` (new boss screen notes).
