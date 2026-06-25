# Spike #1126 — Class system

- **Spike issue:** [#1126](https://github.com/ginderjeremiah/GameServer/issues/1126)
- **Status:** Research complete; direction decided with the project owner; split into implementation sub-issues [#1220–#1226](#implementation-issues). V2 directions split into the standalone follow-up [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219). Not yet implemented.
- **Related:** overlays the proficiency system ([#982](https://github.com/ginderjeremiah/GameServer/issues/982) — class was its deferred decision 3 / bottom-of-doc follow-up); rides the multiple-characters-per-account flow ([#922](https://github.com/ginderjeremiah/GameServer/issues/922), implemented — the create-character screen is where a class is chosen); builds on items-grant-skills ([#980](https://github.com/ginderjeremiah/GameServer/issues/980), merged) for the starter weapon's innate skill; the soft "class-flavoured gear" story routes through the deferred proficiency gear-gate ([#1124](https://github.com/ginderjeremiah/GameServer/issues/1124)).

## Goal

A **class** is chosen at character creation and gives the character a coherent starting identity: a themed **starter kit** (weapon + skills), a **class attribute fingerprint**, and a durable **signature passive**. Every new character requires a class. It is the archetype the rest of the build grows out of — not a parallel progression track.

### The framing that drove the design

The original issue floated several heavyweight ideas — class levels/advancement, in-class branches "cheaper/stronger," class-locked tree nodes, class-gated gear. Most of these were pared away in the spike because they either duplicate or fight systems we already built:

> **A class is a rich character-creation preset with a persistent identity hook — not a runtime gating system.** Almost everything a class "determines" is already a value seeded once at creation; the class just *parameterizes* that seeding. The one durable runtime effect is a **signature passive** fed through the existing attribute-modifier pipeline.

Three things fell out of that framing:

- **Class levels/advancement: dropped.** Proficiencies *are* the advancement layer (#982). A parallel class-XP track is pure redundancy. A class biases the tree it seeds; growth happens there.
- **Class-locked tree nodes: rejected.** It breaks the proficiency invariant that *every node is eventually reachable* (#982 decision 2). A class biases *which paths you start in*, never which you can ever reach.
- **In-class "cheaper/stronger" (the class tax/bonus): deferred, re-framed.** #982 decision 3 tabled this as *premature*, not vetoed. It is compatible with "everything reachable" (it changes efficiency, not access), but it re-couples class into the just-tuned proficiency XP/attribute pipelines and needs a class→path association the kit-derived model otherwise avoids. Held for V2, and re-framed as a **bonus, never a penalty** — a punishing out-of-class tax fights the multi-character design (experimenting on a *different* character is the intended way to explore), whereas a gentle in-class bonus complements it.

## How it works today (what the feature builds on)

Almost the entire feature is *parameterizing seams that already exist*.

- **Character creation is a single domain factory.** `NewPlayerFactory.Create(name, rootSeedSkillIds)` (`Game.Core/Players`) is the one place that answers "what does a new player look like?" — today via the hardcoded constants `StarterSkillCount = 3` and `StartingAttributeAmount = 5` plus uniform per-attribute allocation. It is called from exactly two orchestration sites, `AccountService.CreateAccount` and `AccountService.CreatePlayer` (the #922 multi-character create path). Class slots straight into this factory; the constants *become* class data.
- **The attribute graph composes new contributors for free.** `AttributeCollection` (backend) / `BattleAttributes` (frontend) aggregate modifiers from a tagged `EAttributeModifierSource`; #178 (crit/dodge/block), #528 (graph rework), and #982 (`Proficiency` source) all added contributors the same way. A class's contributions are just more modifiers.
- **Level-scaled attribute distribution already exists — and is exactly the shape the class fingerprint needs.** Enemies scale attributes by `BaseAmount + AmountPerLevel × level` through `AttributeDistribution` (`EAttributeModifierSource.AttributeDistribution`), assembled into the battler on **both** sides. The class **locked base** (below) is the same math applied to the player, derived purely from `(class, level)`.
- **Manual stat allocation is a single guarded path.** `PlayerStatPoints.TryUpdateAttributes` enforces the available-points budget and the non-negative-amount rule over per-attribute `StatAllocation` rows; `StatPointsGained`/`StatPointsUsed` track the budget. The hybrid model narrows this to the **free pool** without touching the path.
- **Items already grant innate skills.** An equipped item's `Item.GrantedSkillId` is an innate, equip-conditional battle skill (#980, merged) — the natural carrier for a class's signature weapon skill (Mage's Staff → Fireball).
- **Inventory/equipment is a per-player aggregate.** `Inventory` holds `UnlockedItems` + `EquipmentSlots` with `TryEquipItem`. New players get **no** items today (`NewPlayer` carries no inventory) — so **starting equipment is the one genuinely net-new mechanism.**
- **Proficiency openness is derived, not seeded per-player.** A tier is open once it accrues XP (or is a root / has its prerequisite maxed / is a satisfied gateway). So a class's paths fall out of its kit automatically: the kit's skills accrue XP on the first won battle and open their paths. `StartsUnlocked` (and the `AccountService.RootSeedSkillIds()` seeding it drives) was a stopgap for "universally open roots" and **retires** once classes own the starting set.
- **Reference data is a uniform pattern.** Each authored catalogue (skills, items, proficiencies, paths) is an EF entity → cache holder → `Volatile` snapshot, entity↔core↔contract mappers, a reader repo, an admin **Workbench** editor in a nav group (Combat / Items / World / **Progression**), and version-keyed socket delivery into the client `staticData` store. A `Class` catalogue follows this whole pattern; its editor joins the **Progression** group beside Proficiency/Path.
- **The conlang "words of power" layer exists.** Proficiencies render a decorative Aetheric-script name via the `WordOfPower` component/font (#1211). A class reuses it as a flat decorative label (no decipher — classes don't level).

## Decisions

Settled with the project owner during the spike:

1. **Class is a creation-time preset with a persistent `ClassId` seam.** It parameterizes `NewPlayerFactory`; the only *runtime* effects are the signature passive and the level-scaled locked base, both via the attribute pipeline. `Player.ClassId` is persisted (display + the seam for V2 effects), but no per-class runtime gating ships in V1.

2. **Class is authored reference data**, not hardcoded — a `Class` catalogue with an admin Workbench editor, id-indexed cache, and codegen'd contract, consistent with every other content type. Content evolves without migrations.

3. **Class is permanent.** Chosen at creation, never changed. A new archetype means a new character (the #922 multi-character flow is the intended vehicle). No respec/class-change machinery.

4. **Starter kit = weapon + one secondary skill + a shared fallback.** A class grants a **weapon** (carrying its signature skill via `Item.GrantedSkillId`, path A) and **one magic/utility skill** for a second path (path B), so every class is lightly bi-pathed from creation. All classes also start with a generic **"punch"** skill that is **path-less** — a pure never-skill-less fallback that earns no proficiency XP and so never dilutes the pie.

5. **Attributes use a hybrid "locked base + free pool" model — which is also the attribute floor.** The floor and the hybrid auto-allocation are one mechanism:
   - **Locked base** = the class's starting spread + a per-level growth vector, expressed as the existing `AttributeDistribution` shape (`BaseAmount + AmountPerLevel × level`) and **derived purely from `(class, level)`**. It is **not reallocatable** — which *is* the floor (you cannot strip your class identity by dumping everything into one stat), and it scales with level so it stays relevant.
   - **Free pool** = a reduced per-level point grant (strawman ~30%) the player allocates through the existing `TryUpdateAttributes` path, on top of the locked base.
   - The player **persists only the free-pool allocations**; the locked base is recomputed from class + level (less stored state than today, and it re-tunes for free when growth vectors change).

6. **A signature passive gives durable, felt combat identity** via a new `EAttributeModifierSource.Class`, assembled into the battler on **both** sides (parity). It is **attribute-scaled** (idea: Warrior damage-reduction scales with Endurance, Mage effect-magnitude with Intellect, Ranger crit with Dexterity), mirroring how skill effects scale off a caster attribute — so the passive and the locked-base fingerprint reinforce each other (the class auto-invests in the attribute its passive feeds on). V1 passives read only snapshot state (flat or attribute-scaled modifiers); conditional/stance passives that read live battle state are V2 ([deferred](#out-of-scope--deferred)).

7. **A class's starting proficiency roots are *emergent from the kit*, not authored.** Granting the kit's skills opens their paths through the existing derived-openness rule (first won battle). **`StartsUnlocked` retires** along with `AccountService.RootSeedSkillIds()` — there are no universally-open roots; discovery comes from the kit + depth. (One consequence noted for V2: an in-class XP *bonus* would need a class→path association, which this kit-derived model otherwise avoids.)

8. **Class-flavoured gear is soft, via the proficiency gear-gate (#1124), not a class lock.** Since a class biases which proficiencies you grow, "heavy armour requires the martial proficiency" gives Warriors-in-plate / Mages-in-robes by default while keeping everything reachable. One gating mechanism (proficiency), no `RequiredClass`.

9. **Flavour: a class word-of-power (conlang) label** (decision: at minimum this), reusing the `WordOfPower` component. A class title/icon is optional, not core.

10. **Transition: existing characters are deleted; every new character requires a class.** No grandfathering (the game is pre-release). `ClassId` is non-nullable on the player; the create-character flow (#922) gains a required class pick.

## Strawman data model (illustrative — shapes, not final numbers)

- **`Class`** (authored reference data): `Id`, `Name`, `Description`, `RetiredAt` (content lifecycle), `Word`/`Pronunciation`?/`Translation`? (conlang label — likely just a decorative `Word`), `StarterSkillIds[]` (permanent, player-owned, selected), `StarterEquipment[]` (`{ ItemId, EEquipmentSlot }` equipped at creation — the weapon carries its signature skill via `GrantedSkillId`), `AttributeDistributions[]` (`{ Attribute, BaseAmount, AmountPerLevel }` — the locked base, same shape as enemy distributions), `SignaturePassive` (`{ Attribute, Amount, ScalingAttribute?, ScalingAmount, ModifierType }` — the attribute-scaled passive).
- **`Player.ClassId`** (new, non-nullable scalar) — the persisted seam.
- **Free-pool persistence** — the existing `StatAllocation` rows + `StatPointsGained`/`StatPointsUsed`, now scoped to the free pool only (the locked base is not stored).

The shared "punch" fallback skill is content (a path-less skill all classes list in `StarterSkillIds`), not a model field. All curves, the auto/free split, growth vectors, and passive magnitudes are a **strawman to tune during balancing** (the #178/#528/#982 convention).

## Changes by area

### A. `Class` reference-data model + admin authoring
The `Class` entity (+ starter skills, starter equipment, attribute distributions, signature passive, conlang label); DbContext config + migration; cache holder + snapshot + `VersionKey`; entity↔core↔contract mappers; reader repository + DI; admin Workbench **class editor** in the **Progression** group (Identity / Starter kit / Attributes / Passive sections, child savers per the `persistEntity` pattern) + add/edit endpoints. Foundation; no hard external dependency (real content needs proficiency content — see I).

### B. `Player.ClassId` seam + class-driven character creation
Add the non-nullable `Player.ClassId` (+ migration); rework `NewPlayerFactory.Create` to take a resolved `Class` and produce the class's starter skills + starting allocation (retiring `StarterSkillCount`/`StartingAttributeAmount`); thread a validated `classId` through `AccountService.CreatePlayer`/`CreateAccount` (reject unknown/retired); **the existing-character reset** (delete all; require a class). Retire `StartsUnlocked` + `AccountService.RootSeedSkillIds()` (roots now emerge from the kit). Depends on **A**.

### C. Starting equipment at character creation (net-new)
`NewPlayer` carries equipped starter items; the persistence layer (`IUsers.CreatePlayer`) builds the inventory/equipment graph (unlock + equip the class's `StarterEquipment`); the weapon's innate skill (#980) comes online at creation. The one mechanism with no precedent in new-player creation today. Depends on **B**.

### D. Hybrid attribute model: locked base + free pool (the floor)
Class `AttributeDistributions` assembled into the battler as a level-scaled, non-reallocatable **locked base** (`BaseAmount + AmountPerLevel × level`, sourced as `Class`/`AttributeDistribution`) on **both** sides; the per-level point grant split so only the reduced **free pool** flows through `PlayerStatPoints.TryUpdateAttributes`; the locked base recomputed from `(class, level)` (not stored). **Offline simulator (#879)** re-derives the locked base as levels cross the window (power is a deterministic function of level — no stationarity violation, since the player still can't *reallocate* while away). Frontend attributes screen renders locked-base vs free-pool; **parity vectors** for a leveled locked base. Depends on **B**.

### E. Class signature passive → attribute pipeline (parity)
New `EAttributeModifierSource.Class`; at battler assembly, generate the class's signature-passive modifier (flat or attribute-scaled) on **both** sides; attribute-breakdown screen shows the `Class` source; **parity vectors**. Depends on **A**, **B**; parallels **D**.

### F. Client: class reference delivery + selection on the create-character screen
`GetClasses` (reference, version-keyed) → `staticData` slot + local cache; codegen contract; the create-character screen (`UI/src/routes/select/CreateCharacter.svelte`, from #922) gains a **class picker** previewing each class's kit (skills, weapon, stat fingerprint, passive, word-of-power); wire the chosen `classId` into the create call; render the conlang label via `WordOfPower`. Depends on **A**, **B**.

### G. Documentation
A **Classes** section in `docs/game-design.md` (preset model, kit, hybrid/floor attributes, signature passive, kit-emergent roots + `StartsUnlocked` retirement, soft gear via #1124, the no-grandfather reset); `docs/backend.md` (class reference data + the creation/locked-base notes); `docs/frontend.md` / `docs/frontend-screens.md` (class picker on the create-character screen). Folded into the area issues on landing. Also: refresh #982's stale `StartsUnlocked` mentions and #922's "not yet implemented" status note.

### H. Authoring the V1 class roster (content)
Design the three V1 classes (strawman: **Warrior** — Sword/Axe, STR/END, martial path; **Mage** — Staff, INT, an elemental path; **Ranger** — Bow, AGI/DEX, a marksman path): their kits, starting distributions, growth vectors, signature passives, and words-of-power. Like #1127 for proficiencies — content that follows the tooling (**A**–**F**) and gives **D**/**E** real data. **Depends on proficiency content (#1127)** so the kit's skills have real paths to feed.

## Implementation issues

Created as native sub-issues of #1126. Dependency order: **A** is the foundation; **B** builds creation on it; **C**/**D**/**E**/**F** branch off **B** (**E** parallels **D**); **H** is content, last (needs the tooling + proficiency content #1127).

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| [#1220](https://github.com/ginderjeremiah/GameServer/issues/1220) — `Class` reference-data model + admin authoring | A | — | large |
| [#1221](https://github.com/ginderjeremiah/GameServer/issues/1221) — `Player.ClassId` seam + class-driven character creation (+ `StartsUnlocked` retirement, existing-character reset) | B | #1220 | large |
| [#1222](https://github.com/ginderjeremiah/GameServer/issues/1222) — Starting equipment at character creation | C | #1221 | medium |
| [#1223](https://github.com/ginderjeremiah/GameServer/issues/1223) — Hybrid attribute model: locked base + free pool (the floor) | D | #1221 | large |
| [#1224](https://github.com/ginderjeremiah/GameServer/issues/1224) — Class signature passive → attribute pipeline (parity) | E | #1220, #1221 | medium |
| [#1225](https://github.com/ginderjeremiah/GameServer/issues/1225) — Client: class reference delivery + class picker on the create-character screen | F | #1220, #1221 | medium |
| [#1226](https://github.com/ginderjeremiah/GameServer/issues/1226) — Author the V1 class roster (content) | H | #1220–#1225, #1127 | medium |

Each issue carries its own unit/integration tests; **D** and **E** (and only those) add **frontend↔backend parity** vectors.

## Out of scope / deferred

Tracked standalone (not sub-issues), so closing the spike doesn't depend on them:

- **V2 class identity — [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219)** *(the follow-up this spike was asked to create)*. Two tabled directions: (1) **in-class XP/power bonus** — reconsider once there's authored tree + class content to balance against; re-framed as a bonus, never a tax; needs a class→path association. (2) **Conditional / "stance" signature passives** — passives that read **live battle state** (Warrior rage = damage rises as health drops; Mage escalation; Ranger first-strike). The only lever that makes a class genuinely *play* differently rather than tweak numbers, but it is **net-new parity-sensitive battle-sim logic** (today modifiers are snapshot-fixed plus timed skill effects; none read current-health ratio), so it needs its own design pass. Serious design considerations — no `claude` tag.
- **Soft class-flavoured gear** — already covered by the deferred proficiency gear-gate (#1124); no class-specific work needed.
- **Class title/icon** — optional flavour beyond the word-of-power label; pick up if wanted.
- **Name** — "Class" is the working term.
