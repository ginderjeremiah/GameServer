# Spike #299 — Additional codegen opportunities & evaluating the current pattern

- **Spike issue:** [#299](https://github.com/ginderjeremiah/GameServer/issues/299)
- **Status:** Research complete; direction decided with the project owner; split into implementation sub-issues (see [Implementation issues](#implementation-issues)).
- **Predecessor:** [#282](https://github.com/ginderjeremiah/GameServer/issues/282) — the one-off codegen of the static attribute-modifier table that prompted this spike.

## Goal

The issue poses three questions, framed by the bespoke #282 work (which taught codegen to emit one off-wire static table):

1. Are there **other areas** the frontend could benefit from codegen, or gaps in how it's used today?
2. Should specific parts of the domain **always** generate mirrored client code regardless of API exposure — e.g. enums, constants, static data?
3. Could **actual logic** be converted from the backend to the frontend (TypeScript or WebAssembly)?

## How codegen works today

`Game.Api/CodeGen/ApiCodeGenerator` is **reflection-driven off the wire contract**: it walks controller endpoints + socket-command DTOs, then emits the TypeScript interfaces, the API/socket maps, and `enums.ts`. Anything the walk reaches is generated; anything it doesn't is invisible to it.

#282 bolted two **off-wire** capabilities onto that walk, both bespoke:

- `StaticModifierWriter` emits `STATIC_ATTRIBUTE_MODIFIERS` from `StaticAttributeModifiers.All`.
- `ApiCodeGenerator.GetDomainEnumDescriptors()` **hand-feeds two enums** (`EModifierType`, `EAttributeModifierSource`) into the walk, because no DTO reaches them but the generated table references them.

The CI codegen-drift check (`run-tests.yml` → `dotnet run … -- codegen` + `git diff --exit-code UI/src/lib/api/types`) guards everything that lands under `UI/src/lib/api/types`.

## Findings — the real theme is "shared domain knowledge mirrored on both sides"

The duplication the spike is circling exists in **four tiers**, guarded very unevenly:

### Tier 1 — Enums (mostly auto-covered; two escape the wire)

Every enum referenced by a DTO is already generated into `enums.ts`. Two domain enums are **not** on the wire and are hand-mirrored on the client:

- **`EEquipmentSlot`** — re-declared by hand in `UI/src/lib/engine/player/inventory-manager.ts`, with the literal comment `//Manually putting this here until codegen gets updated to load this`. The wire only carries `equipmentSlotId: number`.
- **`ERole`** — re-declared in `UI/src/lib/api/roles.ts`. Wrinkle: it is **string-keyed** (`Admin = 'Admin'`, the JWT `role` claim *name*) whereas the backend enum is numeric (`Admin = 1`) and the generated TS enums are numeric — so it is not a straight enum emit.

### Tier 2 — Constants (duplicated magic numbers, zero codegen, already drifting)

| Meaning | Backend | Frontend |
|---|---|---|
| battle tick size | `BattleSimulator.MsPerTick = 40` | `logical-engine.ts tickSize = 40` (+ hardcoded again in the parity test) |
| max battle ms | `BattleSimulator.DefaultMaxMs = MsPerTick * 10000` | `battle-simulator.ts defaultMaxMs = tickSize * 10000` |
| loadout cap | `Player.MaxSelectedSkills = 4` | `battler.ts maxSkills = 4` |
| exp curve | `Player.ExpPerLevel = 100` | `player-manager.ts expPerLevel = 100` |
| stat points / level | `Player.StatPointsPerLevel = 6` | `player-manager.ts statPointsPerLevel = 6` |

`tickSize`/`MsPerTick` **must** match for battle parity yet are kept in step only by hand. The exp-curve pair has **already drifted in logic** (see Tier 4 / the bug below).

### Tier 3 — Static data tables

The #282 attribute-modifier table — the one case already solved, via a bespoke writer.

### Tier 4 — Logic (fully hand-ported; guarded by mirrored parity tests)

The battle simulator is duplicated end-to-end (~566 FE / ~879 BE LOC): `Battler` / `Skill` / `AttributeCollection` / `battleStep` / `BattleSimulator` / `Mulberry32`, plus the inverse category↔slot mappings (`getEquipmentSlotForCategory` vs `EquipmentSlot.GetItemCategory`). This is guarded by **mirrored parity test suites** (`BattleSimulatorParityTests` ↔ `battle-simulation-parity.test.ts`, `BattleAttributesParityTests`, `Mulberry32ParityTests` ↔ `mulberry32-parity.test.ts`) that must be kept row-for-row aligned by hand.

> **An active divergence found during the spike:** backend `Player.GrantExp` is `while (Exp > Level*100)` (loops, strict `>`); frontend `grantExp` is `if (exp >= level*100)` (single level-up, `>=`). At an exact threshold the client levels up and the server doesn't, and a large grant multi-levels on the server but only once on the client. Progression has **no** parity test, so nothing catches this. Filed as a standalone bug ([#304](https://github.com/ginderjeremiah/GameServer/issues/304)).

## Decisions

Settled with the project owner during the spike:

1. **Always-mirror policy: yes for enums + constants (+ static data); no for logic.** These are cheap, declarative, and drift-prone, and the leaks above (the `EEquipmentSlot` comment, the exp-curve drift) prove the current ad-hoc approach doesn't scale. They should be generated regardless of API exposure.
2. **Generalize the off-wire mechanism** rather than adding more one-off hooks. An explicit, self-documenting **opt-in** (a marker attribute or a single curated registry) lets codegen emit chosen domain enums/constants even when no DTO references them, and the existing `GetDomainEnumDescriptors` hardcoded list folds onto it.
3. **Do _not_ pull battle logic to the client via WASM or C#→TS transpilation.** Keep the hand-port + parity-test model. Rationale:
   - The FE battle code is **not** a pure simulator — it's woven into reactive UI (`BattleEngine.logicalUpdate` layers combat-log messages, render cooldowns, stage transitions; `statify` reactivity on the real `Battler`/`Skill` objects). A WASM core would impose a marshalling boundary on the per-tick hot path **and still require** the TS presentation layer around it.
   - A Blazor/.NET-WASM runtime is a multi-MB payload + startup cost for an idle game that wants a light client, and C#-vs-JS floating-point determinism concerns don't improve — you'd trade a code-mirror for a runtime-and-marshalling problem.
   - The parity tests already are the contract for the *logic*. The actual exposed risk is the **unguarded constants feeding that logic** — close that (decisions 1–2) and the hand-port model is sound.
   - No good C#→idiomatic-TS transpiler exists, and the logic is small and stable enough that one isn't warranted.

## Implementation issues

Created as sub-issues of #299:

- **[#304](https://github.com/ginderjeremiah/GameServer/issues/304)** _(bug, claude)_ — Frontend exp/level grant diverges from backend `GrantExp` (`if`/`>=` → `while`/`>`). Standalone; can land independently. Suggests a mirrored progression-parity test.
- **[#305](https://github.com/ginderjeremiah/GameServer/issues/305)** _(enhancement, claude)_ — Generalize off-wire domain codegen (replace the hardcoded `GetDomainEnumDescriptors` with an explicit opt-in), emit `EEquipmentSlot`, and resolve the `ERole` string-key case.
- **[#306](https://github.com/ginderjeremiah/GameServer/issues/306)** _(enhancement, claude)_ — Codegen a shared `game-constants.ts` (tick size, max battle ms, loadout cap, exp curve, stat points) from a single backend declaration and remove the duplicated FE literals (including the parity test's `msPerTick`).

## Where this leaves the policy (for future reference)

| Kind | Source of truth | How the client gets it |
|---|---|---|
| DTOs / wire enums | API + socket contracts | reflection walk (existing) |
| Off-wire domain **enums** | domain enum + opt-in marker | codegen → `enums.ts` (#305) |
| Domain **constants** | single backend declaration + opt-in marker | codegen → `game-constants.ts` (#306) |
| Static **data tables** | domain (`StaticAttributeModifiers.All`) | codegen writer (existing; #282) |
| **Logic** | backend `Game.Core` | hand-port + mirrored parity tests (WASM/transpile rejected) |
