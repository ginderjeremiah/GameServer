# Spike #178 — Player crit / dodge / block (seeded battle RNG)

- **Spike issue:** [#178](https://github.com/ginderjeremiah/GameServer/issues/178)
- **Status:** Design decided; split into implementation sub-issues (see [Implementation issues](#implementation-issues)). Not yet implemented.

## Goal

Give the **player** a chance to land **critical hits** (deal increased damage), **dodge** (fully avoid an incoming attack), and **block** (flatly reduce an incoming attack). Enemies do **not** crit/dodge/block in this version (the issue scopes it player-only) — the asymmetry keeps the RNG draw order simple and is a deliberate seam for later enemy parity.

This is the feature the seeded battle RNG (`Mulberry32` + `BattleSeed`) has been scaffolded for since #96/#180 — it is the first live consumer of the shared random stream, so its central deliverable is a **parity-identical RNG draw order** that the frontend and backend consume in lockstep.

## Current state (what already exists)

- **The attributes already exist, unused.** `EAttribute` (`Game.Core/Enums.cs`) already declares `CriticalChance` (10), `CriticalDamage` (11), `DodgeChance` (12), `BlockChance` (13), `BlockReduction` (14), each marked *"CURRENTLY UNUSED"*. #528 already classified them `Secondary` with display codes (`CRT`/`CRT DMG`/`DOD`/`BLK`/`BLK RED`) and `IsPercentage` flags — so the display metadata is in place, but nothing reads or writes the values.
- **The seeded RNG is built and parity-locked, with no consumer.** `Mulberry32` is ported on both sides and pinned by `Mulberry32ParityTests` ↔ `mulberry32-parity.test.ts`. A cryptographically-seeded `uint BattleSeed` is generated at battle start (`BattleService.CreateBattleSeed`), persisted on `PlayerState.BattleSeed`, and shipped to the client on `EnemyInstance.Seed`. **Nothing draws from it yet** — the simulators ignore it.
- **The attribute graph already supports new contributors.** Both `AttributeCollection` (backend) and the reworked `BattleAttributes` (frontend, #331) compose attributes through one shared path with mid-battle add/remove, so crit/dodge/block compose for free once something feeds them — no new attribute infrastructure is required.
- **`StaticAttributeModifiers` has no formula for crit/dodge/block**, and no gear/mods grant them, so they are currently `0` from every source. Something must feed them or the feature is inert.
- **The damage pipeline is flat-Defense only.** A skill deals `BaseDamage + Σ(attribute × multiplier)` (raw), then the defender's `TakeDamage` subtracts flat `Defense` (clamped at 0). There is no multiply step and no per-hit RNG.
- **Parity discipline.** `BattleSimulatorParityTests` ↔ `battle-simulation-parity.test.ts` must agree tick-for-tick, and the backend **replays the client's reported battle as anti-cheat**. Adding RNG means both sides must draw from the seeded stream in exactly the same order or every fight diverges.
- **#336 waits on this spike.** The deferred skill-effect proc-chance (`ChanceToApply`) explicitly depends on #178 to "define a global RNG draw order across crit/dodge/effect rolls." This spike establishes that order; proc chance later slots a draw into it.

## Decisions

Settled with the project owner during the spike:

1. **Decimal storage convention for percentage attributes — `0.05` means 5%, used as-is in calculations.** No `×100` / `÷100` transform in the battle math. A `CriticalChance` of `0.05` is compared directly against a `[0,1)` RNG draw; a `CriticalDamage` of `1.5` is the crit multiplier **read directly** (a 1.5× hit) — the same base-multiplier convention as the CooldownRecovery rebase below, not a `1 + value` bonus. This settles the storage convention #528 deferred ("5 vs 0.05 — settled when crit/dodge are implemented"). `BlockReduction` is a **flat** value (like Defense), not a percentage.
2. **Rebase `CooldownRecovery` to a base-`1` multiplier.** Every battler gets a static `1.0` base and bonuses are decimals (`0.05` = +5%), so the cooldown multiplier is the attribute **read directly** (drop the `1 + CDR/100` transform). This makes multiplicative modifiers behave intuitively — a `2×` CooldownRecovery buff genuinely doubles charge speed (halves the effective cooldown) instead of nudging `1.09 → 1.18`. It is bundled here because crit/dodge/block establish the same decimal convention. The derived coefficients are rescaled so existing behaviour is preserved exactly (see below).
3. **Crit applies pre-Defense; block is a flat reduction alongside Defense; dodge fully negates.** A crit multiplies the **raw** skill damage before the defender's Defense subtraction; a block adds `BlockReduction` as a second flat reduction in the same clamp as Defense; a dodge zeroes the hit. (See [Runtime semantics](#runtime-semantics-parity-critical).)
4. **Values come from core-attribute derivations *and* gear/effects.** New `StaticAttributeModifiers` rows derive crit/dodge/block from the core attributes (the enum descriptions already hint the mapping: DEX/LUK → crit, AGI → dodge, END → block), so the feature is live from raw allocations; items/item-mods/skill-effects can also grant them (they already carry arbitrary `EAttribute` modifiers). The derivation coefficients are a **conservative strawman to tune** (see [Derivation](#derivation-strawman--tunable)).
5. **Player-only, by gating the rolls on who acts.** Only the player rolls crit (on its own attacks) and only the player rolls dodge/block (on incoming attacks). Implemented by gating on the active/target battler being the player, which generalises cleanly to enemy crit/dodge later without reordering the stream's player draws.
6. **Statistics and challenge hooks are deferred** to a follow-up (not in V1). V1 surfaces crit/dodge/block through the **combat log** only.

## The attribute convention & CooldownRecovery rebase

Percentage- and multiplier-style attributes are stored so they can be consumed without transformation:

| Attribute | Stored | Used in battle as |
| --- | --- | --- |
| `CriticalChance` / `DodgeChance` / `BlockChance` | `0.05` = 5% | probability, compared `rng.Next() < chance` |
| `CriticalDamage` | `1.5` (base `1.5`) | crit damage multiplier, **read directly** (like `CooldownRecovery`) |
| `BlockReduction` | flat (e.g. `20`) | flat damage reduction, like Defense |
| `CooldownRecovery` | `1.09` (base `1` + `0.09` bonus) | cooldown multiplier, **read directly** |

**CooldownRecovery rebase, behaviour-preserving.** Today `CooldownRecovery = 0.4·Agility + 0.1·Dexterity`, consumed as `1 + CDR/100` (so AGI 20, DEX 10 → 9 → ×1.09). The rebase makes the attribute itself the multiplier:

- `StaticAttributeModifiers`: add a `BaseValue` of `1.0` for `CooldownRecovery`, and rescale the derived coefficients by `÷100` → `+0.004·Agility + 0.001·Dexterity`. AGI 20, DEX 10 → `1 + 0.08 + 0.01 = 1.09` — **identical** to today.
- `Battler.GetCooldownMultiplier()` (BE) and `cooldownMultiplier()` (FE, `battle-formulas.ts`) become `return attributes[CooldownRecovery]` (drop `1 + x/100`). Audit every consumer of that formula (the shared display surfaces — skills page, skill tooltips — go through the same function, so they follow automatically).
- The non-buff CDR parity scenarios keep their expected `totalMs` (the value is unchanged); only the CDR-*buff* scenarios re-author their effect amounts (a "double speed" buff becomes `+1.0` additive or a `×2` multiplicative on the base `1`).
- Display: `CooldownRecovery` (base `1`) — and, once crit lands, `CriticalDamage` (base `1.5`) — are multiplier-style attributes, so the Attribute display metadata (`IsPercentage`/`Decimals`) needs a small revisit to render them sensibly (e.g. `1.09×` / `1.5×` rather than `109%` / `150%`). The CDR revisit is in the foundation work; `CriticalDamage`'s follows with the crit display.

## Runtime semantics (parity-critical)

Everything here executes **identically** on `Game.Core/Battle` and its frontend mirror and must be pinned by the mirrored parity matrix. One `Mulberry32`, seeded once from `BattleSeed` at battle start, is threaded through the simulation and advanced in lockstep on both sides.

### RNG draw order

The draw order is a pure function of the **skill-fire sequence** both simulators already agree on — never of attribute magnitudes or roll outcomes — so the per-tick draw count is fixed regardless of stats. Within each tick, in the existing skill-exchange order:

1. **Player's ready skills fire** in loadout order. Each firing skill draws **once for crit** (the player is the only critter), *before* its damage is dealt.
2. **If the enemy survives, the enemy's ready skills fire** in loadout order. Each firing skill draws **twice against the player — dodge, then block — unconditionally** (both draws are always taken, even when the hit is dodged, so the stream never branches on a roll result).

Draws are **unconditional**: a skill that deals zero damage (a pure buff) still draws, and the block draw is taken even after a successful dodge. This makes the draw count `playerFires×1 + enemyFires×2` per tick — fully decoupled from attribute values and outcomes, the most robust parity guarantee. (DoT/HoT and skill effects remain deterministic — they consume no draws in V1.)

### Crit / dodge / block math

- **Crit (player attacking):** roll `c = rng.Next()`. `isCrit = c < CriticalChance`. The raw skill damage is multiplied by `CriticalDamage` (read directly, base `1.5`) on a crit, **then** the enemy's `Defense` is subtracted (clamped ≥ 0). Crit therefore lands before mitigation, so high crit damage punches through Defense.
- **Dodge (enemy attacking the player):** roll `d = rng.Next()`. `isDodge = d < DodgeChance`. On a dodge the hit deals **0** (fully avoided; Defense/block irrelevant).
- **Block (enemy attacking the player):** roll `b = rng.Next()` (always, even if dodged). `isBlock = b < BlockChance`. On a non-dodged block the player's `TakeDamage` subtracts `Defense + BlockReduction` in one clamp (`max(raw − Defense − BlockReduction, 0)`); a dodged hit ignores the block result.

Chances are compared directly; a value `≥ 1` always succeeds and `≤ 0` never does, so no clamp is needed for correctness. A **design cap** on the chances (e.g. dodge can't exceed some ceiling) is a balance overlay, deferred (see [Out of scope](#out-of-scope--deferred)).

### Where it lives

- **Backend:** `BattleSimulator` constructs the `Mulberry32` from the seed and hands it to `BattleContext`. The crit/dodge/block rolls live in the context's damage path, gated on `_isPlayerActive` (player attacking ⇒ crit; enemy attacking ⇒ dodge/block on the player target). `TakeDamage` gains an optional block-reduction parameter. The seed is threaded `PlayerState.BattleSeed → BattleService.SimulateBattle → BattleSimulator`.
- **Frontend:** `BattleEngine.reset` seeds a `Mulberry32` from `enemyInstance.seed`; the headless `BattleSimulator` seeds from the same value; both pass it into `battleStep`, which performs the same gated rolls. `SkillActivation` gains `crit`/`dodged`/`blocked` flags so the live engine can log them (the headless simulator ignores them, staying allocation-identical). The backend needs no such flags in V1 (no stats), but the **rolls and damage results must match**.

## Derivation (strawman — tunable)

New `StaticAttributeModifiers` rows feed crit/dodge/block from the core attributes so the feature is live from allocations. **All coefficients below are a conservative starting point to balance during implementation**, stored in the decimal convention:

| Attribute | Strawman derivation | Example (DEX/LUK/AGI/END = 20) |
| --- | --- | --- |
| `CriticalChance` | `0.002·Dexterity + 0.001·Luck` | DEX 20 → 0.04 (4%) |
| `CriticalDamage` | base `1.5` + `0.0025·Luck` | LUK 20 → 1.55 (×1.55) |
| `DodgeChance` | `0.001·Agility` | AGI 20 → 0.02 (2%) |
| `BlockChance` | `0.002·Endurance` | END 20 → 0.04 (4%) |
| `BlockReduction` | base `2` + `0.5·Endurance` | END 20 → 12 flat |

`CriticalDamage` carries a **base multiplier** of `1.5` so a crit is worth something even before gear — with no crit-damage source a crit would otherwise do `×1` (nothing). Like `CooldownRecovery` it is read directly, so a `×2` multiplicative modifier doubles the crit multiplier intuitively. Gear/item-mods/skill-effects layer on top of these through the normal modifier path. Because the frontend `STATIC_ATTRIBUTE_MODIFIERS` table is **codegen'd** from `StaticAttributeModifiers.All`, the rows mirror to the client automatically.

## Changes by area

### A. Foundation — decimal convention + CooldownRecovery rebase
`StaticAttributeModifiers`: `CooldownRecovery` base `1.0` + rescaled derived coefficients. `Battler.GetCooldownMultiplier`/`cooldownMultiplier` read the attribute directly. Re-author the CDR-buff parity scenarios (both sides). Revisit `CooldownRecovery` display metadata. No new mechanic — a self-contained refactor that establishes the convention crit/dodge/block build on.

### B. Battle runtime — seeded RNG + crit/dodge/block (player only)
Thread `BattleSeed` into both simulators and the live engine; construct the shared `Mulberry32`. Implement the draw order and the crit/dodge/block math gated on the acting battler (player-only). `TakeDamage` gains a block-reduction parameter; `SkillActivation` gains crit/dodge/block flags (FE). New **seeded** parity vectors on both sides covering: a forced crit (chance 1) multiplies pre-Defense; a forced dodge (chance 1) negates; a forced block (chance 1) reduces flatly; the draw-order alignment over a multi-skill exchange; and that enemies never crit/dodge/block (chance 1 on the enemy is ignored). Depends on **A** (the convention/CDR math).

### C. Sourcing — derive from core attributes + verify gear authoring
Add the derivation rows (B's strawman) to `StaticAttributeModifiers` (mirrors to the client via codegen). Verify items/item-mods can author crit/dodge/block (admin attribute pickers include them now that they're "real"). Unit-test the derived composition. Depends on **B** (so the values do something).

### D. UI — combat-log feedback + display
Frontend-only: combat-log lines for crit / dodge / block (reuse `Damage`/`EnemyDefeated` log types or add one if the toggles warrant it). Surface crit/dodge/block on the skill tooltip / battler stat display; the attribute-breakdown screen self-selects them once they have non-combat contributors (per #528). Depends on **B**.

## Battle-parity note

The RNG draw order is the riskiest surface this spike adds: both simulators must seed one `Mulberry32` from the same `BattleSeed` and draw in the identical, outcome-independent order defined above. The existing parity matrices gain **seeded** scenarios (forced-1 / forced-0 chances make outcomes deterministic without a real probability), with identical inputs, seeds, and expected results on both sides — the established convention. The CooldownRecovery rebase (**A**) re-authors existing CDR scenarios but preserves their outcomes.

## Implementation issues

Tracked as sub-issues of #178:

1. **[#797](https://github.com/ginderjeremiah/GameServer/issues/797) — Foundation: decimal percentage convention + CooldownRecovery rebase** *(foundation)* — area **A**.
2. **[#798](https://github.com/ginderjeremiah/GameServer/issues/798) — Battle runtime: seeded RNG draw order + player crit/dodge/block** *(depends on #797)* — area **B**. The parity-critical core.
3. **[#799](https://github.com/ginderjeremiah/GameServer/issues/799) — Sourcing: derive crit/dodge/block from core attributes + gear authoring** *(depends on #798)* — area **C**.
4. **[#800](https://github.com/ginderjeremiah/GameServer/issues/800) — UI: crit/dodge/block combat log + display** *(depends on #798)* — area **D**.

**#797** is the foundation; **#798** is the core mechanic; **#799** and **#800** follow **#798** and can run in parallel. Deferred follow-ups are tracked standalone in **[#801](https://github.com/ginderjeremiah/GameServer/issues/801)** (not a sub-issue, so closing the spike doesn't depend on them).

## Documentation to update on landing

- `docs/game-design.md` — **Character Progression / Attributes**: crit/dodge/block as player mechanics; the decimal percentage convention; the CooldownRecovery multiplier rebase.
- `docs/backend-battle.md` — the **RNG seed** note (it now has a live consumer): the seeded draw order, the player-only gating, and the crit/dodge/block damage rules as parity-critical mechanics.
- `docs/backend.md` / `docs/frontend.md` — battle-parity sections: the seeded RNG stream as a new parity surface, if a cross-cutting mention is warranted.

## Out of scope / deferred

The first five are tracked standalone in **[#801](https://github.com/ginderjeremiah/GameServer/issues/801)** (split into their own issues when picked up); proc chance is #336's.

- **Enemy crit/dodge/block.** Player-only by decision; the gating leaves a clean seam (enemy crit adds a draw on enemy attacks; enemy dodge/block adds draws on player attacks — a stream change, safe since no mid-battle RNG state is persisted across versions).
- **Statistics & challenges** (crit/dodge/block counts, challenge hooks) — a tracked follow-up.
- **Chance caps / diminishing returns** — a balance overlay on the raw chances (e.g. a dodge ceiling); deferred to balancing.
- **Derivation coefficients** are a strawman — expect tuning during implementation.
- **Icon art** for crit/dodge/block — #528 deferred these "until they are real"; generate via the `docs/icon-art.md` pipeline as a small follow-up once the mechanic ships.
- **Skill-effect proc chance (#336)** — builds on the draw order defined here; slots its own draw into the per-skill-fire sequence when picked up.
