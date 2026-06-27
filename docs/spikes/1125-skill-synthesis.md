# Spike #1125 — Skill synthesis (combine skills)

- **Spike issue:** [#1125](https://github.com/ginderjeremiah/GameServer/issues/1125)
- **Status:** Ideation complete; implementation issues created ([#1295](https://github.com/ginderjeremiah/GameServer/issues/1295)–[#1301](https://github.com/ginderjeremiah/GameServer/issues/1301), see [Implementation issues](#implementation-issues)).
- **Related:** spun off from the proficiency spike [#982](https://github.com/ginderjeremiah/GameServer/issues/982) — **supersedes its decision 8** (the seed-skill exception) and removes its cross-path gateways. Builds on the merged item-grant ([#980](https://github.com/ginderjeremiah/GameServer/issues/980)) and skill-rarity ([#979](https://github.com/ginderjeremiah/GameServer/issues/979)) seams.

## Goal

A player-driven **skill synthesis** feature: combine owned skills (plus conditions) into a new skill — `fire + earth → lava`, or fusing two techniques into a stronger one. It started as a way to fill the proficiency tree-seed gap, but the spike reframed it as a feature in its own right.

### The framing that drove the design

Strip synthesis to its primitive and it is *"combine owned reference-data instances (+ conditions) into new ones, deterministically, via authored recipes."* Nothing about that is skill-specific — items are id-indexed reference data with acquisition channels too. So the settled framing is:

> **Synthesis is a deterministic combination engine, skills-first.** V1 wires only the skill→skill path, but the data model is shaped general (`{ inputs, conditions, outputs }` as collections) so it *can* later transform other reference data without a rework.

Two things bound the vision and keep it from sprawling:

- **The defining law (anti-economy):** every transformation is **non-consumptive, deterministic, and gated by possession + mastery — never by consumed resources.** This is what lets the engine grow *wide* (more kinds of inputs/outputs) without ever becoming the material-sink crafting treadmill the game has deliberately rejected (no spendable economy, no respec, no regret).
- **It grows horizontally, not vertically, and only on demand.** When the candidate extensions were priced individually (see [Deferred](#out-of-scope--deferred)), most proved redundant with existing systems or anti-DNA. ~90 % of the value lives in skills. So generality is kept as near-free architectural insurance; concrete extensions are demand-gated future spikes, **not** a roadmap.

### Its role among the game's drivers

Synthesis is a **connective keystone**, not a fourth grind pillar. It has no repeatable loop (non-consumptive, one-time, no economy), so it is deliberately **high-meaning, low-volume**:

- **Peer to challenges** — both are punctuated "direction" systems, mirror images across the agency axis: challenges are the *game* setting goals (→ items / mods / zones), synthesis is the *player* setting them (→ skills). The unlock space stays cleanly split; if they ever unlock the same things, one is miscast.
- **Capstone to proficiencies** — proficiency mastery is a *condition* on recipes, and synthesized skills are what open new proficiency paths. It is the *spend* side of proficiency's *earn*; it interlocks with that system rather than rivaling its identity.
- **Outsized on build expression** — it is the only system where the player *creates* a build piece, and planning a fusion chain toward a target loadout is the most theorycrafty activity in the game.

**Explicit non-goal:** synthesis must never become a *primary* progression — not by dominating the skill-source channels, and not by bolting on consumables/timers to manufacture engagement. Its depth comes from the recipe graph and from feeding the existing loadout/gear opportunity cost, never from a resource treadmill.

## Current state (what already exists)

The feature rides on seams already in place.

- **Skills** are immutable, zero-based-contiguous, id-indexed reference data (`Game.Core/Skills/Skill.cs`), cached via `SkillsCacheHolder` → snapshot → `Volatile` swap, served by `GetSkills`. A synthesized skill is **just an authored skill** — its own rarity, effects, contributions, and word of power. This is *why* authored recipes fit and procedural generation does not (a player-instanced skill has no catalogue id to bake into a battle snapshot, and creates a new frontend/backend parity surface).
- **`ESkillAcquisition`** (`Game.Core/Enums.cs`) is a `[Flags]` set — `Player = 1`, `Item = 2`, `Enemy = 4`. Synthesis adds a fourth channel, `Synthesis = 8`, as authoring intent (which skills are *allowed* to be a synthesis result), validated like the existing Item/Enemy intent-vs-reality bridge.
- **`Player.UnlockSkill`** (`Game.Core/Players/Player.cs`) adds a skill to the unlocked-but-unselected set and is **idempotent** (re-grant is a no-op, raising `SkillUnlockedEvent` only on first unlock) — exactly the shape a one-time synthesis needs.
- **The admin Workbench** authors reference data through `Change<T>` + `ChangeSetProcessor` with child savers (the `persistEntity` pattern) and `[ReloadReferenceCaches]`. A `SkillRecipe` catalogue follows this whole pattern.
- **The proficiency open trigger already does the work.** A path opens when a contributing skill accrues XP in a won battle; acquiring a skill that contributes (a `SkillPathContribution`) is what bootstraps a path. A synthesized `lava` skill opening the lava path is therefore **not a new mechanism** — it is the same trigger.
- **The seed/gateway machinery this spike removes is fully implemented**, not stubbed: `Proficiency.SeedSkillId` + the seed-grant in `ProficiencyRewardService.Open()`, the `ProficiencyPrerequisite` entity + `ProficiencyPrerequisiteGraph` cycle detection + `OpenSuccessors()`/`AllPrerequisitesMaxed()`. Removing it is a deliberate simplification (area C), cheap *now* because the proficiency tree content ([#1127](https://github.com/ginderjeremiah/GameServer/issues/1127)) is unauthored and the game is pre-release.

## Decisions

Settled with the project owner during the spike:

1. **Authored recipes, not procedural generation.** A `SkillRecipe` is reference data; the result is a pre-authored catalogue skill. Procedural / player-instanced skills were explored and declined — they break id-indexed reference data, battle snapshots, parity, retire, and words-of-power for a feature whose value is mostly authorial. (A *formula-generated result* — authored recipe, stats computed at cache-build time — is a viable later authoring convenience that keeps the architecture; deferred.)
2. **Combination engine, skills-first.** Model shaped general; only the skill path built in V1. Generality is insurance, governed by the anti-economy law above; extensions are demand-gated, not roadmapped.
3. **Non-consumptive, one-time, no economy.** Inputs are kept, not consumed; synthesizing is idempotent. The "cost" is owning the inputs + meeting conditions + the choice itself. Opportunity cost stays at the existing ≤4 loadout + gear slots (#982 decision 3), which synthesis *feeds* rather than replacing.
4. **A fourth skill source.** Skills now come from the starter kit, item grants, proficiency milestones, **and synthesis**. `ESkillAcquisition.Synthesis = 8`; a recipe's result must be `Synthesis`-flagged. Once unlocked, a synthesized skill is a normal skill in every other respect.
5. **A recipe is specific input skill ids + optional proficiency-level conditions → one result skill.** Specific-id matching in V1 (precise, fits the reveal UX). Category/predicate matching ("any fire skill + any earth skill") is a strong later extension, deferred.
6. **Inputs are *unlocked* skills only.** Innate item-granted skills (ephemeral, only while equipped) are excluded — no equip-to-synthesize-then-unequip.
7. **Replace all proficiency seeds and remove gateways.** The only nodes that genuinely needed a seed were cross-path gateways (a node with no contributing skills); within-path tiers are already trainable by existing path skills at falloff. So: delete `SeedSkillId` and the `ProficiencyPrerequisite` gateway system; a within-path tier's full-pace native comes from the **previous tier's mastery milestone reward** (default) or an optional **upgrade recipe** (the "fuse two techniques into a stronger one" case); the gateway's "advanced-fire **and** advanced-earth maxed" gate re-expresses as the lava **recipe's** input skills + proficiency conditions. Every path then opens one uniform way — a contributing skill accrues XP.
8. **Pillar role:** connective keystone — peer to challenges, capstone to proficiencies, outsized on build expression, modest on time (see [Goal](#its-role-among-the-games-drivers)). Becoming a primary grind/economy is an explicit non-goal.
9. **Discovery is a conservative hinted reveal.** A recipe is fully shown (with any unmet proficiency condition rendered as the gate) **once the player owns all its inputs**; before that, each owned input that feeds an unrevealed recipe shows a vague *"combines with something"* hint; a recipe with no owned inputs is hidden. Discovery = gathering ingredients.
10. **A dedicated screen** (working title "Synthesis"), reachable from Skills and the Lexicon — not crammed into the greenfield Lexicon tree.
11. **Recipe-graph chaining is the depth source.** A synthesized skill is a normal skill, so it can be an input to a deeper recipe; the graph unfolds across the whole game. Authoring validation guards acyclicity + reachability (the lighter analogue of the gateway cycle check being removed).
12. **Lifecycle & runtime fit.** Synthesize is an explicit, backend-validated player command — **not** part of the idle/offline simulation. Retirement mirrors the content lifecycle: a retired recipe stops being offered, already-synthesized results persist.

## Strawman data model (shapes, not final numbers)

- **`SkillRecipe`** (authored reference data): `Id` (zero-based identity), `ResultSkillId`, child `Inputs` (skill ids, ≥ 1), child `Conditions` (`{ ProficiencyId, MinLevel }`), `RetiredAt`. Inputs/conditions are child collections so the `{ inputs, conditions, outputs }` shape generalizes; V1 outputs a single skill.
- **`ESkillAcquisition.Synthesis = 8`** — authoring intent that a skill may be a synthesis result.
- A cache-time **reverse index** (skill id → recipes it is an input to) drives the client hint state. No per-player table — synthesis availability is derived client-side from owned skills + proficiency levels + recipes.

## Changes by area

### A. `SkillRecipe` reference-data model + acquisition flag + admin authoring ([#1295](https://github.com/ginderjeremiah/GameServer/issues/1295))
The `SkillRecipe` entity (+ input/condition child collections), `ESkillAcquisition.Synthesis`, DbContext config + migration, cache holder + snapshot + `VersionKey` + reverse index, entity↔core↔contract mappers, reader repository + DI, authoring validation (result `Synthesis`-flagged; non-retired inputs/result; acyclicity + reachability), and admin add/edit endpoints. The admin **Workbench recipe editor** is split into [#1299](https://github.com/ginderjeremiah/GameServer/issues/1299) (mirroring how the proficiency editor #1132 followed its foundation). Foundation; no hard dependency.

### B. `SynthesizeSkill` command + execution ([#1296](https://github.com/ginderjeremiah/GameServer/issues/1296))
The player command, authoritative anti-cheat validation (owns every input as an unlocked skill; conditions met; recipe live; result `Synthesis`-flagged), the idempotent `UnlockSkill`, and the client-push payload. Depends on A.

### C. Proficiency simplification: remove seeds + gateways ([#1297](https://github.com/ginderjeremiah/GameServer/issues/1297))
Delete `SeedSkillId`, the `ProficiencyPrerequisite` gateway system (`ProficiencyPrerequisiteGraph`, `DependentsOf`, the gateway branch of `OpenSuccessors`/`Open`/`AllPrerequisitesMaxed`), and the admin seed/gateway authoring; migrations to drop the column + table. Within-path opening stays as notification-only; re-home seed content onto the predecessor tier's max-level milestone reward. Depends on A, B.

### D. Client: recipe reference-data delivery ([#1298](https://github.com/ginderjeremiah/GameServer/issues/1298))
`GetSkillRecipes` version-keyed reference command, codegen contracts, `staticData` slot + local cache. Availability/hints derived client-side. Depends on A.

### E. Client: Synthesis screen (greenfield) ([#1300](https://github.com/ginderjeremiah/GameServer/issues/1300))
A `Synthesis` screen + view-model implementing the conservative hinted reveal and per-recipe state, wiring the Synthesize action. Depends on D, B.

### F. Client: synthesis feedback ([#1301](https://github.com/ginderjeremiah/GameServer/issues/1301))
Synthesized-skill unlock toast + log line on the server push, and a "new recipe available" toast on threshold crossing. Depends on B, D.

## Implementation issues

Created as native sub-issues of #1125:

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| [#1295](https://github.com/ginderjeremiah/GameServer/issues/1295) — `SkillRecipe` model + `Synthesis` flag + admin authoring | A | — | large |
| [#1296](https://github.com/ginderjeremiah/GameServer/issues/1296) — `SynthesizeSkill` command + execution (anti-cheat) | B | #1295 | medium |
| [#1297](https://github.com/ginderjeremiah/GameServer/issues/1297) — Remove proficiency seed skills + cross-path gateways | C | #1295, #1296 | medium |
| [#1298](https://github.com/ginderjeremiah/GameServer/issues/1298) — Client: recipe reference-data delivery | D | #1295 | medium |
| [#1299](https://github.com/ginderjeremiah/GameServer/issues/1299) — Admin Workbench: synthesis recipe editor | A | #1295 | medium |
| [#1300](https://github.com/ginderjeremiah/GameServer/issues/1300) — Client: Synthesis screen (greenfield) | E | #1298, #1296 | large |
| [#1301](https://github.com/ginderjeremiah/GameServer/issues/1301) — Client: synthesis feedback | F | #1296, #1298 | small |

Shape: **#1295** is the foundation → { #1296, #1298, #1299 } branch off it; **#1296** unlocks the proficiency cleanup **#1297**; the player-facing surface **#1300** / **#1301** ride the data delivery + command. Each issue carries its own unit/integration tests.

## Documentation to update on landing

- `docs/game-design.md` — a **Skill Synthesis** section (the combination-engine framing, the anti-economy law, recipes, the hinted reveal, the connective-keystone role); update the **Skills** sources to add synthesis as a fourth channel; update the **Proficiency** section to remove seeds + gateways and describe the uniform open trigger. (With A–C.)
- `docs/backend.md` — the `SkillRecipe` reference-data + cache note, the `SynthesizeSkill` command + authoritative validation, and the seed/gateway removal. (With A–C.)
- `docs/frontend.md` / `docs/frontend-screens.md` — the Synthesis screen (hinted reveal) and the synthesis feedback. (With E, F.)

## Out of scope / deferred

Each candidate extension was priced individually; most do not survive scrutiny, which is *why* the engine stays skills-first. Tracked here, not as issues (demand-gated, not roadmapped):

- **True procedural / player-instanced skills** — explored and declined (new parity surface, loses authored balance/rarity/words-of-power).
- **Formula-generated result stats** — viable authoring convenience layered on authored recipes; revisit if recipe authoring volume warrants it.
- **Category/predicate input matching** — the strongest *within-skills* enhancement ("any fire + any earth"); fuzzier reveal UX, deferred.
- **Item-forging** (combine items → better item) — **redundant**: gear already has challenges (unlock) + mods (customize); a third path overlaps both.
- **Imbuing** (put an owned skill onto gear) — the one genuinely *new* capability, but high balance risk (it can collapse the loadout scarcity the opportunity-cost model rests on) and really a distinct feature. Its own spike if ever wanted.
- **Reagents / materials / consumable economy** — anti-DNA (the rejected economy).
- **Transmute / non-skill outputs** — redundant with recipes, or imply regret.
- **Initial synthesis content authoring** (recipes + result skills) — design/content work that follows this tooling and the proficiency content (#1127); gets its own issue when those land.
- **Name** — "Synthesis" is provisional.
