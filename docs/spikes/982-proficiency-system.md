# Spike #982 — Proficiency system (name provisional)

- **Spike issue:** [#982](https://github.com/ginderjeremiah/GameServer/issues/982)
- **Status:** Foundation implemented (#1114 / #1115 / #1116 merged). **Refined mid-build to add the Path system** ([Paths](#paths-refinement)) — linear sequences of proficiencies as a first-class contribution unit — slotted in before the player-facing UI. The refinement reworks the merged model and accrual via two new sub-issues (#1160, #1161) and adjusts the remaining open issues in place; see [Path refinement issues](#path-refinement-issues).
- **Related:** builds on [#980](https://github.com/ginderjeremiah/GameServer/issues/980) (items grant skills — [#1100](https://github.com/ginderjeremiah/GameServer/issues/1100)/[#1101](https://github.com/ginderjeremiah/GameServer/issues/1101) merged); [#979](https://github.com/ginderjeremiah/GameServer/issues/979) (skill rarity) feeds tier-weighted XP; supersedes the challenge→skill reward path.

## Goal

A **proficiency** is a mastery track for a *category* of skills. Using the skills of a category (in won battles) levels its proficiency; levels pay out **passive bonuses, new skills, and access to deeper proficiencies**. Proficiencies form a **tech tree (DAG)**: a small set of roots is open from the start, and maxing a node opens more advanced, more specialized children (which may require one or several maxed parents). It is the Elder Scrolls "learn by doing" loop, adapted to an idle game where combat is automated.

### The framing that drove the design

The original issue framed proficiency as a replacement for *challenges as the skill-unlock path*. The spike **reframed it**: the literal "use a skill N times → unlock a skill" gate is **already expressible today** with a per-skill `SkillsUsed` challenge (the per-skill statistic and the `SkillsUsed` challenge type both exist). So proficiency is only worth its cost if it is **more than an unlock gate** — a first-class progression axis. The settled shape:

> **Proficiency is a mastery _layer_, not a skill _source_.** Skills keep coming from the world (starter kit, items, milestones); proficiency converts *usage* of the skills you've collected into **bonuses + gear access + tree progression.**

This also resolves the idle-game wrinkle. Because combat is automated, "use a skill to level it" reduces to "have it equipped and win with it" — so the design leans on three things to keep that from being a bland timer: a **scarce loadout** (only equipped skills train), **depth-scaled power** (specializing beats spreading), and **difficulty-scaled XP** (you progress by fighting appropriate content, not parking on trivial mobs).

## Current state (what already exists)

The feature rides on seams already in place.

- **Skills** are id-indexed immutable reference data (`Game.Core/Skills/Skill.cs`), served via `GetSkills`, with **no categorization** today. [#980](https://github.com/ginderjeremiah/GameServer/issues/980) adds an `ESkillAcquisition` `[Flags]` enum (`Player`/`Item`/`Enemy`) and lets an equipped item grant an **innate skill** (`Item.GrantedSkillId`) that is **active while equipped, additive to and exempt from the 4-skill loadout cap** — the opportunity cost living at the *gear slot*. That item-skill channel is exactly the **branch-opener / keep-equipped seed** this spike needs.
- **Per-skill statistics already exist.** `EStatisticType.SkillsUsed`/`DamageDealt` carry an `EEntityType.Skill` breakdown, and `RecordBattleCompleted` already writes per-skill rows from `BattleStats.SkillStats` (`{skillId → uses, damage, …}`). So the **per-skill "which skills fired this battle" data proficiency XP needs is already produced** at battle completion.
- **The challenge system already gates unlocks on statistic thresholds** (`Challenge.RewardSkillId` → `Player.UnlockSkill` via `ChallengeRewardService`), including the per-skill `SkillsUsed` stat — which is *why* proficiency must be more than that.
- **The attribute graph composes new contributors for free.** `AttributeCollection` (backend) / `BattleAttributes` (frontend) aggregate modifiers from stat allocations + equipment + mods, assembled at `BattleSnapshot.ToBattler`. A permanent **proficiency bonus is just another `AttributeModifier` source** (new `EAttributeModifierSource.Proficiency`), exactly as #178 added crit/dodge/block and #528 reworked the graph. `StaticAttributeModifiers` codegens to the client.
- **Battle completion** is `BattleService.EndBattleVictory → Player.RecordBattleCompleted → BattleCompletedEvent → BattleStatisticsEventHandler` (the async progress-persistence hook). Offline rewards run a separate **end-of-window consolidation** (`OfflineProgressSimulator`) that already folds in statistics + challenges.
- **Per-player collections** follow one pattern (`PlayerStatistic`/`PlayerChallenge`): an EF row keyed by `(PlayerId, …)`, a `PlayerProgress` domain aggregate with **dirty-tracking**, a write-behind `ProgressUpdatedEvent` to the Redis player queue, and a sliding-TTL progress cache. `PlayerProficiency` mirrors this exactly.
- **Reference data** is cached per set (`SkillsCacheHolder` → snapshot → `Volatile` swap), mapped entity↔core↔contract, and **authored content (skills, challenges, items) is created at runtime through the admin Workbench**, not seeded in migrations. A new `Proficiency` catalogue follows this whole pattern.
- **Frontend:** game screens are a single `GAME_SCREENS` array (`screen-defs.ts`); reference data is delivered by socket commands (`GetSkills`, `GetChallenges`, …) into the `staticData` store with version-keyed local caching; player data loads per-screen (`GetPlayerChallenges`, …). Contracts are codegen'd (`contracts.ts`). **No tree/graph/node-progression UI exists** — the proficiency tree view is greenfield (the only SVG precedent is the attribute radar).

## Decisions

Settled with the project owner during the spike:

1. **Proficiency is a mastery layer over world-sourced skills.** Skills come from: the **starter kit** (permanent), **item innate skills** (equip-conditional, via #980), and **proficiency milestones** (permanent). **Challenges no longer grant skills** — `RewardSkillId` is removed; existing skill-granting challenges re-home to milestones or skill-bearing item rewards. The "beat a boss → learn a new school" path survives by routing through an **item reward** (the boss drops a staff/shard whose innate skill opens the proficiency); no capability is lost.

2. **Proficiencies form a tree (DAG).** Each node is a mastery track with a **low cap (~10, tunable)**. Each level grants a **small bonus**; **milestone levels** (strawman: 5 and 10) grant a **larger bonus and/or a skill**. **Maxing** opens **child nodes**; a child may require **one or several** maxed parents. Every node is **eventually reachable** (bonuses are permanent and additive), but **depth scales power superlinearly**, so a specialist mid-journey is stronger than a generalist.

3. **"Costly to go wide" is enforced by depth-scaled power + the slot/time gate — no new economy.** The scarce **loadout (≤4 selected)** plus the **gear slots** (innate item skills, per #980) rate-limit how many tracks you train at once; deep-tier bonuses dwarf shallow ones, so breadth is strictly slower to power. Rejected as cost levers: a **spendable resource** (adds an economy + respec question) and a **class tax** (couples to the class system prematurely). Multi-prerequisite nodes are **tree structure** (themed gateways), not the cost mechanism. **Class is a later overlay** (see [Deferred](#out-of-scope--deferred)).

4. **XP is earned on victory, fixed-pie split across represented proficiencies, difficulty-scaled.** Total proficiency XP per won battle is **constant** (a fixed pie), **scaled by the same difficulty curve as `DefeatRewards`** (trivial enemies pay little — anti-grind). It is split across the proficiencies **represented** in that battle — a proficiency is *represented* if at least one of its contributing skills **fired** in the won fight (selected **or** innate item skill) — with each proficiency's slice proportional to `Σ(skillTierWeight × contributionWeight)`. Consequences, all intended:
   - **Fixed pie ⇒ diversifying dilutes.** A loadout of 4 skills from one proficiency trains it at full rate; 4 skills across 4 proficiencies trains each at a quarter rate. Focus is faster than spread, down to the micro level.
   - **Representation (not damage/kills) is what makes bootstrap work.** A freshly-opened branch's weak seed skill earns XP just by being in a fight you win on your *other* skills — the same property that defeats the fast-cooldown grind exploit (a fast skill earns no more pie than a slow one).
   - **Tier weight (from #979 rarity)** paces deep proficiencies so they aren't an eternal trickle-grind on a starter skill; low-tier still earns **nonzero** XP (slow, never walled). Until #979 lands, tier weight is flat (`1`).
   - **Refined by [Paths](#paths-refinement):** the split now happens at the *path* level, routed to the path's current tier, and each path's slice is scaled by an **absolute on-tier efficiency** (the falloff) — so a stale loadout earns less in absolute terms, not just relatively.

5. **A skill contributes to one _or more_ proficiencies, with weights (multi-contribution, in V1).** Modeled as a join (`skillId → {proficiencyId, weight}`), the same shape as the tag system. This is built up front because it resolves a late-game tension single-FK creates — *your strongest skills sitting in maxed proficiencies would earn zero progression*, forcing you to bench them to grind — and because it lets a sparse early loadout push several tracks at once. Most skills will list one proficiency in practice. **Refined by [Paths](#paths-refinement):** a contribution now targets a **path** at a **home tier** (not an individual proficiency), and within-path spread is the falloff curve rather than a row per node — so "multi-contribution" now means a skill feeding **multiple paths** (a genuine cross-school skill), which is the case that actually matters.

6. **What opens a proficiency:** (a) **acquiring a skill that contributes to it** (an item seed, a starter skill, or a multi-contribution milestone skill that also touches it); (b) **maxing its prerequisite proficiencies** (tree); (c) **starting proficiencies at creation** (a small **2–3 root** archetype set, class-biased later — discovery comes from *depth*, not a wide root layer).

7. **What a proficiency outputs:** per-level + milestone **attribute bonuses** (via the modifier pipeline); **permanent milestone skills** (via `UnlockSkill`); **child-node unlocks** on max; and (fast-follow) **gear gates**. Bonuses are baked into the **battle snapshot at battle start** like stat allocations, so a level gained while idling takes effect on the next battle.

8. **Seed-skill handling is asymmetric by context.** An **item-opened** branch trains via the item's **equip-conditional** seed skill (you keep the seeding item equipped) until an **early first milestone** grants a permanent skill. A **tree-opened** branch (the "advanced fire + advanced earth → basic lava" case, which has no world source) grants a **permanent seed skill on unlock**. Invariant preserved: **a proficiency is never reachable without a way to train it.** (Skill synthesis could later supersede the tree-seed exception — see Deferred.)

9. **XP is computed backend-authoritative at battle completion, not in the tick loop.** Like statistics/rewards today, proficiency XP is computed when the battle resolves (live: `BattleStatisticsEventHandler`; offline: the consolidation pass) and **pushed to the client** (mirroring challenge-completion). It is **not** part of the 40 ms seeded simulation, so it adds **no battle-parity surface** — the only parity-sensitive piece is the *attribute bonus*, which both sides already compute from player state at snapshot time.

## Paths (refinement)

Added **after the foundation (#1114–#1116) merged**, before the player-facing tree UI — the cheapest moment to restructure, since nothing player-visible had shipped. A **path** is a linear sequence of proficiencies sharing an identity arc (Fire Magic → Inferno Magic), made a first-class unit so a skill ties to the *sequence* once instead of to every node. It reworks the merged model (#1160) and accrual (#1161); the open downstream issues are adjusted in place.

10. **A path is a linear spine; the tree (DAG) connects spines at gateways.** Within a path, proficiency *N+1* opens when *N* is maxed, so the linear edges are **implicit in `PathOrdinal`** — no authored prerequisite rows. `ProficiencyPrerequisite` is reserved for **cross-path gateways** (lava = advanced-fire + advanced-earth → the lava path's first tier). A standalone proficiency is a path of length 1.

11. **Tiers are distinct-identity nodes, not renamed bands.** Each proficiency in a path keeps its own name, description, bonuses, milestone skills, and seed skill — Fire Magic → Inferno Magic, *not* "Basic/Advanced Fire". The node stays the unit of leveling, bonuses, and `PlayerProficiency` progress (so #1115's per-proficiency persistence is untouched); the path is only the connective tissue and the contribution/routing unit.

12. **A skill contributes to a path at a home tier; its pull falls off with tier distance.** The join becomes `SkillPathContribution (SkillId, PathId, HomeTier, Weight)`. A skill's pull on the path's frontier tier is `Weight × falloff(frontierOrdinal − HomeTier)`, `falloff(0) = 1` decaying for deeper tiers (geometric `FalloffBase^distance`, authored per path — strawman). A standard fire skill (home tier 0) trains Fire Magic fully and Inferno at a fraction — one row + a curve, not a row per tier. Distance is always ≥ 0 (a tier's native skills are gated behind that tier opening, so a skill never trains a tier *below* where it was acquired).

13. **XP routes to the current tier, and falloff is an _absolute_ multiplier — the pie is a ceiling, not a constant.** At completion each represented path resolves to its **frontier tier** (lowest un-maxed, unlocked node). The pie is split across paths by their **falloff-free attention** (`Σ tierWeight × Weight` — how much of the loadout points at the path), then each slice is scaled by the path's **on-tier efficiency** (its contribution-weighted average falloff); the un-earned remainder is **not minted**. So coasting on stale skills trains slower in *absolute* terms — the intended cost — distinct from the maxed-tier slice-drop the routing already removed. Still computed server-side off the tick loop: no parity surface, and the offline path replays identically (same loadout → same falloff).
    - **Seed skills stop a fresh tier from stalling.** Opening a tier grants its native seed skill (home tier = that tier, falloff 1), so a newly-revealed Inferno starts at full pace while old fire skills *supplement* at the discount. The pull is "fill the loadout with the new tier's natives" — never walled, just slower if you coast.

14. **Hiding is a client-render choice, not enforced.** Locked tiers are simply not drawn (no teaser for now — an easy later add). Definitions stay on the **shared, broadcast reference channel**; no per-player filtering. A tampered client can read ahead — an accepted, deliberate non-goal. The client derives the unlocked set from player levels + the path/gateway structure it already holds.

## Strawman data model (illustrative — shapes, not final numbers)

- **`Path`** (authored reference data — *added by the [refinement](#paths-refinement)*): `Id`, `Name`, `Description`, `FalloffBase` (the per-tier contribution decay), `RetiredAt`. Its ordered tiers are the proficiencies carrying its `PathId`, sequenced by `PathOrdinal`.
- **`Proficiency`** (authored reference data): `Id`, `Name`, `Description`, `MaxLevel` (~10), `XpCurve` (per-level thresholds or formula params), **`PathId` + `PathOrdinal`** (its tier within the path), `PerLevelModifiers` (attribute modifiers applied × level), `Milestones` (`{ Level, Modifiers, RewardSkillId? }`), `SeedSkillId?` (granted when this tier opens), `RetiredAt`. *(The original `StartsUnlocked` root flag has since been **retired** — a character's starting roots are emergent from its class kit, spike #1126.)* `Prerequisites` (proficiency ids that must be maxed) now hold **cross-path gateway** edges only — within-path order is implicit in `PathOrdinal`.
- **`SkillPathContribution`** (join — *replaces `SkillProficiency`*): `SkillId`, `PathId`, `HomeTier`, `Weight` — a skill feeds a path at a home tier; multiple rows = a cross-**path** (multi-school) skill.
- **`PlayerProficiency`** (per-player — unchanged): `(PlayerId, ProficiencyId)`, `Level`, `Xp`.

All curves, caps, milestone levels, and bonus magnitudes are a **strawman to tune during implementation** (the #178/#528 convention).

## Changes by area

### A. Proficiency reference-data model + skill contributions + admin authoring
The `Proficiency` entity (+ prerequisites, per-level/milestone bonus specs, seed skill) and the `SkillProficiency` contribution join; DbContext config + migration; cache holder + snapshot + `VersionKey`; entity↔core↔contract mappers; reader repository + DI; admin Workbench **proficiency editor** (Identity / Levels & milestones / contributions sections, child savers per the `persistEntity` pattern) and add/edit endpoints; a `progression` workbench group entry. Foundation; no hard external dependency (rarity weight is additive later).

**Refinement ([#1160](https://github.com/ginderjeremiah/GameServer/issues/1160)):** add the `Path` entity, give `Proficiency` a `PathId`/`PathOrdinal`, replace the `SkillProficiency` join with `SkillPathContribution` (+ `HomeTier`), reduce `ProficiencyPrerequisite` to cross-path gateways, and add a Workbench **path editor** (ordered tiers + `FalloffBase`) alongside the proficiency editor. Reworks the merged #1114.

### B. PlayerProficiency progress persistence
`PlayerProficiency` EF entity keyed `(PlayerId, ProficiencyId)`; fold `level`/`xp` into the `PlayerProgress` aggregate with its own dirty set; extend `ProgressUpdatedEvent` + the write-behind upsert handler + the progress cache model; contract + `GetPlayerProficiencies`. Mirrors `PlayerStatistic`/`PlayerChallenge`. Depends on **A**.

### C. XP accrual + leveling (the core mechanic)
At battle completion compute the **fixed-pie, difficulty-scaled** XP and split it across the proficiencies whose contributing skills **fired** (consuming `BattleStats.SkillStats`); apply to `PlayerProficiency`, handling multi-level gains against `XpCurve`. Wire the **live** path (`BattleStatisticsEventHandler`) and the **offline** consolidation pass (constant loadout over the window). Tier weight flat (`1`) initially. Emit a result payload (per-proficiency xp gained / new level / milestones crossed) for the client push. Depends on **A**, **B**.

**Refinement ([#1161](https://github.com/ginderjeremiah/GameServer/issues/1161)):** route each path's contribution to its frontier tier, weight by `falloff(distance)`, and split the pie by attention × **absolute on-tier efficiency** (the pie becomes a ceiling — coasting evaporates the remainder). Reworks the merged #1116's `ProficiencyXpCalculator` + `BuildContributions`.

### D. Milestone & unlock effects + open triggers
On level-up: cross **milestones** (grant `RewardSkillId` via `UnlockSkill`, flag bonus milestones), and on **max** open child nodes. **Open triggers:** open a proficiency when a contributing skill is acquired (item/starter/milestone — the item path builds on #980/#1101), when prerequisites max (granting the `SeedSkillId`), and seed the **root** set at character creation. Adjusts what `ESkillAcquisition.Player` *means* (milestone-granted, not challenge-granted). Depends on **A**, **B**, **C**; the item-acquisition open trigger builds on [#1101](https://github.com/ginderjeremiah/GameServer/issues/1101) (merged).

### E. Proficiency bonuses → attribute pipeline (parity)
New `EAttributeModifierSource.Proficiency`; at battler assembly, generate modifiers from each proficiency's `Level` (`PerLevelModifiers × level + reached milestone modifiers`) and feed them into `AttributeCollection` / `BattleAttributes` — **on both sides** (player proficiency levels ship to the client as player state). Parity vectors covering a leveled proficiency's bonus. Depends on **A**, **B**; can run parallel to **C**.

### F. Client: proficiency reference + player data delivery
`GetProficiencies` (reference, version-keyed) + `GetPlayerProficiencies` (player) socket commands; codegen contracts; `staticData` slot + a `playerProficiencies` store; reference-data registration + local cache. Depends on **A**, **B**.

### G. Client: Proficiencies tree screen (greenfield)
A `Proficiencies` entry in `GAME_SCREENS` + screen/view-model rendering each **path as a linear spine** (tiers most-advanced → root), with cross-path **gateways** as the DAG edges and **locked tiers hidden** (client-derived from player levels + the path/gateway structure — no teasers). Per-tier level/progress, milestone markers, seed/training states; a path **rail** and a per-tier **detail inspector**. Greenfield inline SVG/flex, no graph dependency.

A later design refinement reframed the screen as **"The Lexicon"**: each tier is a **word of power** in the Aetheric conlang (`WordOfPower`, font #1211) that **deciphers as it levels** — undeciphered → *pronunciation* (at `ceil(maxLevel/2)`) → *translation* (at `maxLevel`; both thresholds derived, not stored). This adds three authored strings (`Word`/`Pronunciation`/`Translation`) to the `Proficiency` reference data — no path-level word, the rail reuses the root tier's. Broken into five sub-issues under umbrella #1120 — see [Area G breakdown](#area-g-breakdown). Depends on **F**; conlang authoring rides the proficiency editor #1132, real content is #1127.

### H. Client: XP / level-up / milestone feedback
On the server push: update the store, toast level-ups and milestone unlocks (mirroring challenge-completion), and add combat-log lines for proficiency XP / level / unlock. Depends on **C**, **D**, **F**.

### I. Remove skill rewards from challenges + re-home
Remove `Challenge.RewardSkillId` (entity + migration, contract, mapper, `ChallengeRewardService` skill branch, admin reward picker, `challenge-unlocks.ts` + `ChallengeTooltip`); re-home existing skill-granting challenges to **milestones** or **skill-bearing item rewards** (content). Supersedes #1100's challenge-reward validation of the `Player` flag. Depends on **D** (milestone skill unlocks must exist to re-home to).

## Implementation issues

Created as native sub-issues of #982:

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| [#1114](https://github.com/ginderjeremiah/GameServer/issues/1114) — Proficiency reference-data model + skill contributions + admin authoring | A | — | large |
| [#1115](https://github.com/ginderjeremiah/GameServer/issues/1115) — PlayerProficiency progress persistence | B | #1114 | medium |
| [#1116](https://github.com/ginderjeremiah/GameServer/issues/1116) — XP accrual + leveling on battle completion (live + offline) | C | #1114, #1115 | large |
| [#1117](https://github.com/ginderjeremiah/GameServer/issues/1117) — Proficiency bonuses → attribute pipeline (parity) | E | #1114, #1115 | medium |
| [#1118](https://github.com/ginderjeremiah/GameServer/issues/1118) — Milestone & unlock effects + proficiency-open triggers | D | #1114, #1115, #1116 (+ #1101, merged) | large |
| [#1119](https://github.com/ginderjeremiah/GameServer/issues/1119) — Client: proficiency reference + player data delivery | F | #1114, #1115 | medium |
| [#1120](https://github.com/ginderjeremiah/GameServer/issues/1120) — Client: Proficiencies tree screen (umbrella — see [Area G breakdown](#area-g-breakdown)) | G | #1119 | large |
| [#1121](https://github.com/ginderjeremiah/GameServer/issues/1121) — Client: XP / level-up / milestone feedback | H | #1116, #1118, #1119 | medium |
| [#1122](https://github.com/ginderjeremiah/GameServer/issues/1122) — Remove skill rewards from challenges + re-home | I | #1118 | medium |

**#1114** is the foundation. **#1116** is the core mechanic; **#1117** (bonuses) and **#1119** (data delivery) branch off **#1115** in parallel. **#1118** ties leveling to skill/branch unlocks. **#1120**/**#1121** are the player-facing surface. **#1122** is the transition cleanup, last because it needs milestones to re-home onto. Each issue carries its own unit/integration tests; **#1117** (and only it) adds **frontend↔backend parity** vectors.

### Path refinement issues

The [Paths refinement](#paths-refinement) lands after #1114–#1116 (merged). It reworks the foundation via two new native sub-issues and adjusts the open downstream issues in place (no new issues for those):

| Issue | Reworks / depends on | Scope |
| --- | --- | --- |
| [#1160](https://github.com/ginderjeremiah/GameServer/issues/1160) — Path model: entity, contribution-join rework, admin authoring | extends #1114 | large |
| [#1161](https://github.com/ginderjeremiah/GameServer/issues/1161) — Home-tier falloff routing + absolute-falloff XP accrual | reworks #1116; depends on #1160 | medium |

Adjusted open issues: **#1118** — open-triggers become path-aware (within-path reveal-on-max is implicit from `PathOrdinal`; gateways stay explicit; each tier grants its seed skill on open). **#1120** — the tree screen renders paths as spines and **hides locked tiers** (client-derived, no teasers for now). **#1117** / **#1119** / **#1121** / **#1122** are largely unaffected (per-tier bonuses, broadcast delivery, feedback, and the challenge cleanup are unchanged by Paths). When #1160 / #1161 land, refresh the Proficiency section of `docs/game-design.md` for the path model + absolute falloff.

### Area G breakdown

The Proficiencies tree screen (#1120, area G) is an **umbrella**; the work is split into five native sub-issues that also fold in the **Lexicon / words-of-power decipher** layer from the post-spike design refinement (design: `docs/designs/game/proficiencies/`). The backend pair is independent; the client work fans out from the view-model.

| Issue | Part | Depends on | Scope |
| --- | --- | --- | --- |
| [#1214](https://github.com/ginderjeremiah/GameServer/issues/1214) — Deliver Path reference data to the client (`GetPaths`) | path delivery (closes an area-F gap) | — | small |
| [#1215](https://github.com/ginderjeremiah/GameServer/issues/1215) — Conlang "words of power" fields on the `Proficiency` reference model | decipher data | — | small |
| [#1216](https://github.com/ginderjeremiah/GameServer/issues/1216) — Screen scaffold + view-model (state derivation) | logic core | #1214 | large |
| [#1217](https://github.com/ginderjeremiah/GameServer/issues/1217) — Lexicon path rail + tier spine rendering | main visual | #1216, #1215 | large |
| [#1218](https://github.com/ginderjeremiah/GameServer/issues/1218) — Word-detail inspector pane | detail pane | #1216, #1215 | medium |

Shape: #1214 ∥ #1215 → #1216 → { #1217, #1218 }. The screen builds and unit-tests against fixtures; visual verification needs the proficiency editor (#1132) + authored content (#1127). Admin authoring of the conlang fields is #1132's responsibility (cross-referenced there).

## Documentation to update on landing

- `docs/game-design.md` — a **Proficiency** section: the mastery-layer model, skill sources (starter/item/milestone; challenges no longer grant skills), the tree + low cap + milestones, victory-based fixed-pie difficulty-scaled XP, multi-contribution, the two-layer (loadout + gear) opportunity cost, bonuses via the attribute pipeline. (With **A**–**E**, **I**.)
- `docs/backend.md` — the proficiency reference-data + `PlayerProficiency` persistence note, and the **battle-completion XP hook** (backend-authoritative, off the tick loop). (With **A**–**D**.)
- `docs/frontend.md` / `docs/frontend-screens.md` — the Proficiencies "Lexicon" tree screen (paths-as-spines, words-of-power decipher) and the XP/milestone feedback. (With **G**, **H**.)

## Out of scope / deferred

Tracked standalone (not sub-issues, so closing the spike doesn't depend on them):

- **Tier/rarity-weighted XP** ([#1123](https://github.com/ginderjeremiah/GameServer/issues/1123)) — folds into **C** once [#979](https://github.com/ginderjeremiah/GameServer/issues/979) lands (flat weight until then).
- **Gear-gating by proficiency** ([#1124](https://github.com/ginderjeremiah/GameServer/issues/1124)) — an item `RequiredProficiency` gate (the zone-`UnlockChallengeId` pattern). Strong **fast-follow**, not V1 core; it completes the gear↔proficiency spiral (gear trains a proficiency → proficiency gates better gear).
- **Skill synthesis** ([#1125](https://github.com/ginderjeremiah/GameServer/issues/1125)) — a player-driven "combine two skills into a stronger one" crafting feature (generalize beyond magic — e.g. fusing two sword techniques). Its own spike; would also give the player *agency* in the tree-seed gap, later superseding the seed-skill exception in decision 8.
- **Class system** ([#1126](https://github.com/ginderjeremiah/GameServer/issues/1126)) — a later overlay biasing the tree (in-class branches cheaper/stronger, class-locked nodes, class-determined roots). Strong synergy with multiple-characters-per-account (each character a different specialization). Its own spike.
- **Initial tree content authoring** ([#1127](https://github.com/ginderjeremiah/GameServer/issues/1127)) — designing the actual roots, contributions, milestones, and curves. Design/content work that follows the admin tooling in **A**; also needed to give the **C**/**E** tests real data.
- **Name** — "Proficiency" is provisional.
