# Content Design

The living design plan — the "content bible" — for the game's **authored content**: zones, enemies, items, item mods, skills, proficiency paths, classes, challenges, and the progression arc that ties them together. This is the **why and the plan**. It is the companion to three other things, and the boundary between them matters:

- **[game-design.md](./game-design.md)** owns the **systems and mechanics** (how damage types resolve, how proficiency XP accrues, how zones gate). This doc does **not** re-explain mechanics — it links to them.
- **The admin Workbench + Postgres** owns the **exact values** (this enemy's attribute spread, this item's stats). This doc does **not** mirror point values — see the boundary rule below.
- **Spike [#1390](https://github.com/ginderjeremiah/GameServer/issues/1390)** owns the **tooling** around content (source-controlled export/seed, the progression-graph lint).

## What belongs here — and what doesn't

**DO put here:**
- Design intent and the player-experience goals each piece of content serves.
- The **progression arc**: the zone order, what each zone *teaches*, and the unlock graph (which challenge gates which zone, which unlock grants which item/skill).
- **Curves and pacing** as *shapes and targets* (the difficulty ramp, time-to-milestone), not per-record numbers.
- Rollout order for damage types, proficiency paths, and classes.
- Open questions and the parking lot.

**DON'T put here:**
- **Exact point values** for individual records (an item's `+5 Strength`, an enemy's base HP). Those live in the Workbench/DB and would rot here the instant you tune them. Refer to content **by name**; give a number only when the *shape* is the design (a curve, a band, a ratio).
- **Mechanics already in game-design.md.** Link, don't restate.
- Per-piece authoring rationale that is specific to one record — that belongs in the proposed per-entity `DesignerNotes` field (#1390), with the piece itself.

> **This doc is exempt from the "keep it concise / prune" rule** that governs the architecture docs (backend.md, frontend.md, …). A content bible is *expected* to grow detailed and stay that way. Prune only what is wrong or abandoned, not what is merely long.

---

## 1. Design pillars

The rubric every piece of content is judged against.

- **The core idle loop is build-craft, then let it run.** The player designs a build in pursuit of **challenges and/or proficiencies**, sets it running, and returns to the results. The *progression itself* is one half of the reward; its **impact** — primarily **beating bosses and advancing zones** — is the other. Content has to feed both: progression to chase, and gated payoffs that make the progression *mean* something.
- **Build decisions matter on two axes, and both must pay off:**
  - **Depth** (specializing) → **compounding synergies** between equipment, skills, and proficiencies that multiply a focused build's power.
  - **Breadth** (diversifying) → **fusion opportunities** ([skill synthesis](./game-design.md#opening-tiers-and-milestones)) that unlock *unique abilities* a narrow build can't reach.
  - Authoring implication: every system should offer a depth reward *and* a breadth reward, so neither playstyle is a trap.
- **Every build should have a viable path.** The end goal is a progression line for *any* conceivable build, with **"hidden" cross-system synergies** the player can discover that make even unlikely builds work. No build is a dead end — if a build has no path, that is a content gap to fill, not a player mistake.
- **Capability is earned by investment, not spoon-fed.** When a zone pressures a mechanic, the *tools to answer it* come from the player leaning into the systems that zone trains — challenges completed and proficiencies leveled by playing it — **not** from guaranteed boss drops. A player who invests in sustain naturally unlocks more sustain; a player who doesn't, doesn't. Content's job is to **create the pressure and lay the path**, then let the player's choices unlock the answer.
- **Idle cadence:** offline rewards are currently **identical** to playing live, capped at **~10 hours** (subject to change — see [Offline Rewards](./game-design.md#offline-rewards)). The **gap between zones starts short and lengthens** as the game goes on: fast, frequent wins early; longer-horizon commitments later.

---

## 2. The progression spine

Content here is a **graph, not a list**: a zone is gated by a challenge, the challenge rewards an item/mod, an item grants a skill, a skill trains a proficiency path, and class kits + synthesis feed in from the side. The single most important authoring discipline is keeping that graph **reachable** — no zone gated behind an uncompletable challenge, no challenge rewarding a retired item, no skill flagged for a channel that never grants it. (That correctness check is the progression-lint in #1390; this section is where you *plan* the graph so it's coherent in the first place.)

The **spine** is the zone order. Each zone should introduce **one new thing to learn** and gate the next zone behind clearing its boss.

### Two ways to teach: zones vs. tutorial blurbs

Not every lesson earns a whole zone. Teaching is delivered two ways, and choosing the cheaper one keeps the early arc punchy:

- **Zone design** — the zone's enemies and boss *embody* the lesson. Reserve this for mechanics that need **sustained pressure** to internalize (you have to live with DoT for a while to learn to counter it).
- **Tutorial blurb** — a short, in-context explanation. Use this for mechanics a player can grasp from **one good explanation** (what a crit is, how cooldowns charge). _(Tutorials are a not-yet-built feature — see the parking lot.)_

A single zone can also fold a lesson into its **boss** rather than its whole spawn table (the boss is where a new threat first bites).

### Planned zone order

> Strawman below — compressed so the opening moves fast. The point is the *teaching sequence*, not a 1:1 zone-per-mechanic mapping.

| # | Zone | Level band | Teaches (via) | Entry gate | Key unlocks |
|---|------|-----------|----------------|------------|-------------|
| 1 | _(opener)_ | 1–? | core loop + weapon signature (**spawns**); **first elemental → resistance** (**boss**) | always open | first gear ladder; opens native paths; starts a resist path |
| 2 | _(blight / DoT)_ | ?–? | **sustain** — DoT bypasses Toughness, so resist + regen matter (**zone**) | clear Z1 boss | gear-ladder step; sustain earned via proficiency/challenge investment (not a boss drop) |
| 3 | _(first build wall)_ | ?–? | **commitment** — a boss an unfocused build can't out-DPS before the timeout (**boss**) | clear Z2 boss | milestone skill; a `RequiredProficiency`-gated weapon |

**Taught by blurb, not zone:** crit/dodge **variance**, the idle-loop/UI basics, cooldown charging _(add others as they come up)_.

> **Crit enabler must precede the Precision path.** Crit is opt-in (rework [#1398](./spikes/1398-utility-in-proficiency-system.md) / [#1425](https://github.com/ginderjeremiah/GameServer/issues/1425)): `CriticalChance` has no attribute base, so it is `0` until a **crit-granting item or skill** is equipped, and the Precision proficiency (which scales chance *multiplicatively*) trains nothing and rewards nothing without one. So a crit **enabler** must be introduced **alongside or before** the Precision path becomes trainable — never author a Precision reward into a zone whose toolkit contains no crit enabler. This applies to every delivery archetype (each is `0` until its enabler is in reach); crit is just the first instance. Reflect the enabler's arrival in the introduction schedule below when the Technique paths are scheduled.

**Why this shape (strawman rationale, for discussion):** the elemental/resistance lesson is folded into the **Zone 1 boss** (the first hit that isn't pure Physical), so resistance is taught without spending a zone on it. Zone 2 is the one mechanic that genuinely needs a *whole zone* — **DoT attrition**, which you only learn by surviving it. Zone 3 is the first real **build wall**: a timeout boss that an unfocused build can't beat, cashing in the depth-vs-breadth pillar. The [exp-reward difficulty band](./game-design.md#experience-rewards) means each level band should be tuned so the zone's enemies sit near the player's expected power as they pass through.

### The introduction schedule — what's available, per zone

A zone is only as good as the **toolkit the player brings into it**, and that toolkit is defined by the proficiency paths trainable and the challenges completable *up to that point*. Authoring a zone in isolation guesses at that; this table makes it explicit. The zone arc (§2), the [proficiency rollout](#4-proficiency-paths--organizing-strategy--rollout) (§4), and the [challenge web](#8-challenges--statistics--organizing-strategy) (§8) are **three views of one progression graph** — keep them synchronized here.

> Read a zone's row to see exactly what the player already has when they arrive. Author the zone against *that*, not an imagined toolkit. A cell appears only once the content that delivers it exists (a path is "trainable" only when something in-zone trains it). **The Zone 1 row is the generic floor** — each class opens different *starting* paths (§5), but early zones are class-agnostic in what they *require* (a zone provides its own counters), so no class needs more than this floor to progress.

| Zone | Damage types live (as content) | Paths now trainable | Challenge families available | Player tools by entry |
|------|--------------------------------|---------------------|------------------------------|------------------------|
| 1 | Physical + weapon leaves | Martial (weapon leaf + `Physical`) | gate; basic kill-mastery | starter kit only |
| 1 boss → | + **Fire** (first elemental) | + Fire-resist (player); Fire-offense (enemy-side) | + "survive the boss" feat | resist training begins |
| 2 | + **Poison** (DoT) | + Poison/DoT-resist; Restoration (if healing) | + **sustain mastery** → unlocks sustain gear/mods | resist tier 1; basic regen access |
| 3 | _(no new type — a depth test)_ | _(deepen existing leaves)_ | + **feats** (timed / under-leveled); `RequiredProficiency`-weapon gates | compounding bonuses; first **synthesis** recipe in reach |

---

## 3. Power & pacing curves

> Record the *shapes* and the *targets* here. The live numbers are `GameConstants` / `ServerGameConstants` and the per-record distributions in the Workbench — name them, don't copy them.

> **Sequencing — plan content first, tune numbers second.** The exact curve values are deliberately deferred: they need playtest experimentation, and pacing targets are far easier to set *after* the content arc (zones, challenges, the unlock graph) is laid out than before it. Lay out the arc; derive the numbers from it.

- **Zone cadence:** time-to-clear per zone **starts short and lengthens** across the game (design pillar) — quick early wins, longer later commitments. This is the curve that shapes how the whole arc *feels*. _(Record the intended cadence — e.g. zone 1 in minutes, with each subsequent zone multiplying the expected dwell time — once the arc is drawn.)_
- **Character level / XP curve:** _(growth shape; stat points granted per level — note the free-pool grant is smaller than the class's locked base, see [Classes](./game-design.md#classes))_
- **Enemy power scaling:** every enemy is `BaseAmount + AmountPerLevel × level` per attribute (same shape as a class's locked base). Decide the **per-zone power target** and let level-band + distribution hit it. _(Record target power per zone band.)_
- **Difficulty & reward:** the `DefeatRewards` band is `1×` inside ±20% of matched power, quadratic outside, capped at a 2× ratio ([details](./game-design.md#experience-rewards)). Tune zone bands so the intended enemy sits inside the band for a player progressing normally.
- **Proficiency pacing:** per-tier curve is `BaseXp × XpGrowth^level` with a per-tier `MaxLevel`; per-victory magnitude is `ServerGameConstants.ProficiencyXpPerVictory`. _(Record target tiers-per-zone so path progression tracks the zone arc.)_
- **Pacing milestones — time-to-X targets:**
  - First session (first ~15 min): _(reach level __, clear zone 1, unlock __)_
  - First ~10 idle hours (the offline cap): _(roughly reach zone __ / level __)_
  - _(add longer-horizon checkpoints)_

---

## 4. Proficiency paths — organizing strategy & rollout

The damage-type taxonomy ([leaves, categories, weapon leaves](./game-design.md#damage-types)) yields a *large* potential path space — offense and resist for every leaf and category, plus the combat-event paths. Authoring all of it bespoke is unmanageable, and most of it shouldn't exist at launch. The organizing strategy keeps it tractable and tied to the zone arc.

**Organizing principles:**

1. **Group paths into families** mirroring the taxonomy — gives the player a mental map and us a balancing unit:
   - **Martial** — weapon leaves under `Physical` (Swordsmanship, Axemanship, …) + the `Physical` umbrella.
   - **Elemental** — Fire / Water / Earth / Wind offense + the `Elemental` umbrella.
   - **Affliction** — Bleed / Poison / Burn offense + the `DoT` umbrella.
   - **Warding** — the resist paths (per-type + Elemental / DoT / Physical umbrellas).
   - **Technique** — the combat-event paths: Precision (crit), Evasion (dodge), Restoration (heal), Retribution (reflect).

2. **Three structural roles encode the depth/breadth pillar — leaf, umbrella, gateway.**
   - **Leaf** paths (Fire, Sword, Poison) max fastest for a *focused* build (spread dilutes each leaf) and give steep, type-specific compounding — **depth**.
   - **Umbrella** paths (Elemental, DoT, Physical) are a **design choice**, not automatically open. Two models: an *open baseline* trained continuously from any family member (the category key fires on every member's hit), **or** a **late-game capstone** unlocked by advancing the leaves beneath it (e.g. an Elemental-resist umbrella earned by leveling the individual element resistances) — a reward for **breadth within a family**. Current lean: **capstone, not open at start** (especially the resist umbrellas).
   - **Cross-family gateways** (lava = adv. Fire + adv. Earth) require multiple advanced leaf tiers maxed — **breadth across families**, the rarest fusion a mono-build can't reach.
   - Author the bonuses to match: leaf = steep compounding; umbrella capstone = a family-wide reward worth the spread; gateway = a unique fusion ability.
   - **Open mechanics question:** a capstone umbrella bound to the auto-firing category key would accrue XP — and therefore *open* — on the first family hit, defeating the gating. A true capstone needs either a non-auto activity key or a gateway rule that suppresses accrual until prerequisites are met. Flag for the proficiency-system owner (parking lot).

3. **Tier depth: ~5+ per leaf, loosely tracking the rarity ladder.** A leaf path runs ~5+ tiers with a Common→Mythic *feel* (a **loose**, not strict, correspondence to `ERarity`), authored **incrementally** as the arc reaches them — not all up front. A shared leveling-curve template keeps balance comparable; reserve bespoke depth for a few "hero" paths.

4. **The fusion-gateway graph is sparse and deliberate.** Don't author every cross-combination — curate a small set of high-identity gateways (lava = adv. Fire + adv. Earth, steam = Fire + Water, …) as the breadth→fusion payoff. Each is a discovery; rarity is what makes it special.

5. **Rollout is derived from the zone arc.** A path opens by being *trained* (openness is derived from progress — [opening tiers](./game-design.md#opening-tiers-and-milestones)), so a path is meaningfully available only once the zone arc introduces its activity as content. The proficiency rollout **is** the introduction schedule (§2) — don't introduce Wind paths before Wind appears.

> Rollout table — one row per path. `Intro` = where the content that trains it first appears.

| Path | Activity key | Family / book | Intro | Tier arc | Milestone skills | Notes |
|------|--------------|---------------|-------|----------|------------------|-------|
| _(Swordsmanship → …)_ | Sword | Martial / offense | Z1 | … | … | weapon leaf; also trains `Physical` umbrella |
| _(Fire Magic → Inferno)_ | Fire | Elemental / offense | Z1 boss (enemy-side) | Fire Magic → Inferno → … | _(skill at tier-N max)_ | umbrella `Elemental`; gateway lava = adv. Fire + adv. Earth |
| _(Fire-resist)_ | Fire | Warding / resist | Z1 boss | … | … | trains from *taking* Fire |
| _(Poison)_ / _(Poison-resist)_ | Poison | Affliction / Warding | Z2 | … | … | DoT leaf; umbrella `DoT` |
| _(Restoration)_ | heal | Technique / event | Z2 (if healing) | … | … | trains from healing |
| _(Evasion / Precision)_ | dodge / crit | Technique / event | blurb-taught; opens on first proc | … | … | not zone-gated |

### Delivery archetypes & the commitment rule (the utility question)

> Resolves the former "utility has no proficiency home" question — full reasoning in spike [#1398](./spikes/1398-utility-in-proficiency-system.md).

The families above (Martial / Elemental / Affliction / …) are *damage-type* niches. Layered orthogonally on them are **delivery archetypes** — the *style* damage is dealt in — and this is the other axis of build differentiation (differentiation scales as **leaves × archetypes**, both extensible). A path is earned by a **committed** archetype, and identities are authored to be commitments, never universal free value:

- **The commitment rule.** A delivery archetype earns a proficiency path only if it is *committed* — reached through an **opt-in enabler** (gear/skill) with a real opportunity cost, measured as **excess over a floor that is naturally `0` when uncommitted**. *Universal* effects that benefit every build for free (a raw STR/CDR buff, all-damage%, raw EHP) get **no** path — their payoff is the paths they amplify (and, for cadence, farm throughput). So a "utility" buff is in or out based on **commitment, not on being a buff**; author the universal residue as conditional or tradeoff effects, never as free value.
- **The opt-in-multiplicative template.** Every archetype follows one pattern: a **flat enabler** in items/skills with **no attribute base** (`0` until opted in) + a **multiplicative** Technique path (`0 × mult = 0`, inert for the uncommitted) + a **tallied magnitude** the path trains on. Crit is instance #1 (reworked to this shape — see spike); DoT and Reflection already fit.
- **The tally is a normalized _marginal_, not the full hit** (settled in the [spike](./spikes/1398-utility-in-proficiency-system.md#follow-on-refinement--the-overlay-tally-shape-normalized-marginal)). An **overlay** archetype (crit, Hex, Sunder, Momentum, Cull — ones that ride a hit that would have landed anyway) tallies the **extra damage it enabled**, measured **add-one-in** against a fixed baseline (so stacked overlays don't inflate each other) and as the **absolute** damage enabled (so a mitigation debuff doesn't train faster against high-resist enemies), passed through a concave normalization so magnitude investment rewards without a runaway. This is applied to **crit too** ([#1448](https://github.com/ginderjeremiah/GameServer/issues/1448) is the worked reference); Reflection/DoT are not overlays and keep their current tallies. _Tuning watch-point:_ a mitigation debuff's *value* is hyperbolic in enemy resistance, so author enabler strengths so a debuff can't erase a resist-gated boss.
- **Roster — committed, implementable archetypes** (not deferred content): **Momentum** (in-battle escalation), **Hex** (vulnerability/debuff), **Sunder** (anti-mitigation) — all expressible on today's effect system — and **Cull** (execute), which additionally needs a new damage-calc conditional. The **engine** for each (path + enabler + tally) is ready to build now, following the crit reference; only each one's **content placement** — which zone/challenge introduces its enabler — follows the arc, and that authoring step never gates the mechanic.
- **Attribute-concentration is content, not a path.** "All-in STR → STR synergies" is served by opt-in **attribute-scaling** skills/items (orthogonal to damage type, so it distinguishes STR from a DEX physical build and does not fight magic-school spread), *not* by a proficiency track — the taxonomy stays type/delivery-based.

---

## 5. Classes

A class is a [character-creation preset with a persistent identity hook](./game-design.md#classes) — the *first* content a new player touches.

**Kit model:** each class starts with **one weapon** (carrying its signature skill via `GrantedSkillId`) and **one secondary off-skill**. **Punch** is not a kit slot but the **virtual fists' `Unarmed` signature** — fielded only when no weapon is equipped (the [weapon-match gate](./game-design.md#weapon-match-gate)'s bare-handed case) and training the `Unarmed`/`Martial` mastery like any other weapon signature. So a class kit = **weapon + 1 secondary**.

**Early zones are class-agnostic by design.** A zone provides and teaches the tools to beat it (Zone 2 introduces its own counters), so no class is locked out of early progression regardless of its kit — clearing Zones 1–2 never depends on a class-specific tool. Class shapes a character's *starting roots and identity*, not its *access*; the schedule's per-class variance is in starting roots only.

> **Class: _(name)_** — _(word of power)_
> - **Fantasy / identity:** _(one line)_
> - **Weapon:** _(type → signature skill)_   ·   **Secondary:** _(off-skill → the path it opens)_
> - **Attribute fingerprint:** _(locked-base lean — `BaseAmount` + `AmountPerLevel` per attribute)_
> - **Signature passive:** _(flat or attribute-scaled combat-identity bonus)_
> - **Paths it opens:** _(the paths its weapon + secondary open on first XP)_

### Strawman launch roster  [DRAFT]

Four classes, each anchored on a different core attribute and weapon family:

| Class | Weapon (→ signature) | Secondary | Attribute | Opens (starting roots) |
|-------|----------------------|-----------|-----------|------------------------|
| **Swordsman** | Sword | _TBD_ | STR | Swordsmanship (+ `Physical`) |
| **Bowman** | Bow | Wind skill | DEX | Archery + Wind |
| **Wizard** | Fire staff (→ Fireball) | _TBD_ | INT | Fire (elemental) |
| **Knight** | Blunt / Club | Regen (heal) | END | Club mastery + Restoration |

- Covers four of the six core attributes (STR / DEX / INT / END); **AGI and LUK** are deliberately left to the Technique paths (dodge / crit), not class identities.
- Each secondary should open a *second* path, so a character starts with **two roots** (weapon family + secondary) — seeding the depth-vs-breadth choice from level 1.
- **The TBD secondaries open their second root via a *committed* effect, not a universal buff** ([#1398](./spikes/1398-utility-in-proficiency-system.md), §4): a raw STR/CDR buff trains nothing (it is a universal amplifier), so a secondary meant to open a root must carry a *committed* hook — a small damage/event portion or an opt-in archetype enabler. A class *may* instead take a pure-utility secondary and deliberately start single-rooted.
- **Minor open content question:** the **staff** has no weapon-leaf in the current taxonomy (`Sword/Axe/Bow/Club/Dagger/Unarmed`) — decide whether the Wizard's staff is a new `Staff` leaf or reuses an existing one; its *signature* is Fire-typed regardless.

---

## 6. Skills

> Plan skills **by school/path**, not as a flat dump. For each: its school, acquisition channel(s), the role it plays (opener / sustain / burst / utility), and — for synthesized skills — its recipe. Stats (damage portions, multipliers, effects, cooldown) live in the Workbench; capture **intent and identity** here.

- **Acquisition channels** ([details](./game-design.md#skills)): starter kit · item grant (`GrantedSkillId`, must be `Item`-flagged) · proficiency milestone · synthesis recipe. Every skill's authored `Acquisition` flags must match a real grant (the lint/authoring validation enforces this).
- **The synthesis web:** _(which owned skills combine into which result, and the proficiency-level conditions that gate the recipe — this is the discovery layer, see [skill synthesis](./game-design.md#opening-tiers-and-milestones))_
- **Words of power register:** _(keep the romanized `Word` / `Pronunciation` / `Translation` here so the invented script stays internally consistent across skills and paths)_

---

## 7. Items & mods

> The gear ladder, slot by slot, zone by zone. Capture **what role each piece plays** and **what unlocks it**, not its stat block.

- **The gear ladder:** _(per equipment slot, the rough upgrade cadence across zones — when the player should expect a meaningful weapon/armor step)_
- **Weapons:** every weapon declares a `WeaponType` **and** a `GrantedSkillId` (its own-type signature) — no pure stat-sticks ([invariant](./game-design.md#items-item-mods-and-tags)). Plan each weapon *with* its signature skill as a unit.
- **`RequiredProficiency` gating:** _(which gear is locked behind a proficiency level — soft class-flavour gating)_
- **Item mods & tags:** the mod pool, and which **tags** decide what mods can apply to what items. _(List the tag vocabulary and the mods keyed to each tag.)_
- **What unlocks each item/mod:** items and mods are unlocked through **challenges** — record the challenge → reward mapping here (it's half of the unlock graph in §2).

---

## 8. Challenges & statistics — organizing strategy

Challenges are the **unlock layer** and the **zone gates**, and the primary vehicle of the *capability-is-earned* pillar. Organize them by **intent**:

1. **Gates** — "clear zone N's boss." One per zone; the next zone's `UnlockChallengeId` points at it ([zone progression](./game-design.md#zone-progression--locking-zones-behind-challenges)). The spine.
2. **Mastery** — accumulating, activity-based ("defeat N of X", "deal N Poison damage", "survive N seconds of DoT"). These unlock the **tools** for a playstyle (gear / mods) and are **tied to the zone's theme**, so the pressure a zone applies and the reward for answering it sit in the same place — the sustain challenges live in the DoT zone. This is *how* investment unlocks capability.
3. **Feats** — skill-expression ("win within N seconds", "win without taking damage", "beat the boss under-leveled"). They unlock prestige / build-defining / cosmetic rewards and reward *mastery*, not grind. Feats are where the Zone 3 build wall hands out its best prizes.

**Discipline:**
- **Theme-match mastery to zones** — a zone's available challenges should reward the playstyle that zone pressures. The introduction schedule (§2) is the source of truth for which families are available where.
- **No orphan rewards / unreachable gates** — every non-gate challenge unlocks something reachable; exactly what the [#1390](https://github.com/ginderjeremiah/GameServer/issues/1390) progression-lint checks.
- **Statistic coverage** — list the statistic each family needs and flag any **new** one required ("at or below" feats need a minimized statistic, see [goal direction](./game-design.md#challenge-goal-comparison-direction)). _(e.g. a "DoT survived" feat needs a stat that tracks it.)_
- **Density convention** — roughly per zone: **1 gate + a handful of mastery + 1–2 feats**, to keep pacing even and authoring consistent.

> | Challenge | Intent | Backing statistic | Goal | Gates / unlocks |
> |-----------|--------|-------------------|------|------------------|
> | _(clear Z1 boss)_ | gate | BossesDefeated (this boss) | 1 | unlocks Z2 |
> | _(survive N Poison in Z2)_ | mastery | _(DoT-taken stat)_ | N | unlocks a sustain mod |
> | _(win Z3 boss within N s)_ | feat | FastestVictory (this boss) | ≤ N | unlocks _(prestige reward)_ |

---

## 9. Per-zone template

> Copy this block per zone into §10. It mirrors the zone's authorable surface (spawn table, boss, gate) and adds the design intent the Workbench can't hold.

```
### Zone N — <name>  [STRAWMAN | DRAFT | LOCKED]
- **Level band:** Lmin–Lmax   ·   **Order:** N
- **Theme & fantasy:** <one line>
- **Teaches (the one new thing):** <the single mechanic/decision this zone introduces>
- **Entry gate:** <challenge that must be complete to enter, or "always open">
- **Spawn table:** <enemies + rough role; which damage types they pressure — NOT exact stats>
- **Boss:** <name> — <the build-check it represents>   ·   **Clear challenge:** <name>
- **Unlocks on clear:** <items / mods / skills the zone's challenges grant>
- **Proficiency relevance:** <which paths this zone trains / opens>
- **Designer notes:** <intent, balance watch-points, pitfalls — the "why">
```

---

## 10. Zones

> Worked entries. The first is filled as a **strawman** to demonstrate the template — replace it with your real opener.

### Zone 1 — _(opener)_  [STRAWMAN]
- **Level band:** 1–?   ·   **Order:** 1
- **Theme & fantasy:** the threshold just past safety — weak, common creatures the player clears to find their footing.
- **Teaches (via):** the **core idle loop** + your **weapon's signature attack** (spawns); the **first elemental hit → resistance matters** (boss).
- **Entry gate:** always open (the first zone is never gated).
- **Spawn table:** 2–3 low-power, single-skill enemies dealing **Physical** only; even spawn weights. This is the safe sandbox where the loop is learned.
- **Boss:** the first enemy that deals an **element** (e.g. Fire) instead of pure Physical — a still-simple fight whose elemental hits are the player's first "mitigation isn't one-size-fits-all" moment, and the seed of a resist path.   ·   **Clear challenge:** _Defeat the Zone 1 boss._
- **Unlocks on clear:** the first armor upgrade (one slot) and unlocks **Zone 2** (its `UnlockChallengeId` points here).
- **Proficiency relevance:** opens the class's **native weapon path** (e.g. Swordsmanship) and its secondary path the first time those skills deal damage; the boss's element starts the matching **resist** path training.
- **Designer notes:** must be clearable by **every** class's starter kit — this is the tutorial-by-doing zone. Keep enemy power inside the ±20% reward band for a level 1 starter so early XP feels rewarding. The boss should *introduce* resistance, not gate on it — a player with zero resist still wins, just feels the chip damage. **Decided:** the boss deals plain **Fire** — a player's first boss should teach exactly one thing (resistance); DoT gets its own whole zone next, so don't muddy the first fight with it.

### Zone 2 — _(blighted marsh / DoT)_  [DRAFT]
- **Level band:** _(TBD — picks up where Z1 ends)_   ·   **Order:** 2
- **Theme & fantasy:** a blighted, poisoned wetland — nothing here kills you fast, it kills you *slowly*. The threat is attrition, not a big hit.
- **Teaches (via):** **sustain / effective-HP-over-time** (whole zone). Enemies apply **Poison** (a DoT debuff). The lesson the player must internalize: **DoT bypasses the Toughness curve** ([damage types](./game-design.md#damage-types)), so a pure-bulk build that leaned on Toughness gets punished — the only answers are **type resistance** (Poison/DoT-resist) and **HealthRegen** (HoT). Toughness ≠ everything.
- **Entry gate:** clear the Zone 1 boss.
- **Spawn table:** a **poisoner** (low direct damage, stacks `PoisonDamagePerSecond` on the player) paired with a straightforward bruiser — so the player feels DoT attrition without being one-shot while they learn it.
- **Boss:** a heavy DoT-dealer; the build-check is *"do you have a sustain answer — resist and/or regen — and did you bring it?"* A glass cannon can still race it down; a Toughness-only tank stalls out and times out.   ·   **Clear challenge:** _Defeat the Zone 2 boss._
- **Unlocks on clear:** a standard gear-ladder step (a slot upgrade) and **Zone 3**. Deliberately **not** a sustain-specific reward — per the *capability-is-earned* pillar, sustain tools are unlocked by *investing* in what the zone trains (the resist/Restoration proficiencies and any sustain challenges), not handed out at the boss. The player who leaned into sustain already has more of it by the time the boss falls; the player who brute-forced it gets a normal upgrade and moves on.
- **Proficiency relevance:** this is the zone's real reward vector. Surviving the DoT trains **Poison/DoT-resist** (incoming book) just by being hit; running a heal trains **Restoration**. The sustain *tools* a player earns here flow through those tracks and matching challenges — the embodiment of the capability-is-earned pillar.
- **Designer notes:** the trap to avoid is **walling** — a baseline sustain answer must be *acquirable before* entering (Z1 rewards, an early mod, or the resist path that began training at the Z1 boss), so Z2 teaches the player to *lean into* sustain rather than springing an unsolvable problem. Balance watch-point: tune Poison stacks so an unprepared player *notices* the attrition and adapts, instead of dying with no idea why. Keep **depth→compounding out of this zone** — that's Zone 3's job (the build wall); Zone 2 only has to teach that sustain exists and matters.

### Zone 3 — _(first build wall)_  [PLANNING — content horizon]

Zone 3 is less a zone to author in isolation than a **content horizon**: it's the point where builds first diverge, so the surrounding content (proficiency depth, the first synthesis, gated gear, feats) must be planned to *exist* by here. Author that toolkit first; the zone block then falls out.

- **Divergence thesis — Zone 3 forces a *win condition*.** It's a timeout boss an unfocused build can't out-DPS, and outlasting only earns a draw — so a build with no way to actually *close* the fight stalls. Zone 3 is where the player commits to one, and the routes split along the pillar:
  - **Depth route** — deepen a single leaf (compounding damage) + the matching `RequiredProficiency`-gated weapon. Focused classes (Swordsman, Wizard).
  - **Breadth route** — the **first synthesis** fuses owned skills into a stronger ability that punches through. Branching classes (Bowman, Knight).
  - **Reflection route** — `DamageReflection` is the tank's *deterministic* win condition: it converts the otherwise-inevitable timeout draw into a kill, and is strongest exactly against a hard-hitting boss ([mitigation rework](./game-design.md#critical-hits-dodge--damage-reflection)). This is how a defensive build wins **without abandoning defense** — the Knight's on-theme path through the wall.
  - Only a build with **no** win condition (an uncommitted spread, or pure passive bulk with no reflection) stalls to a draw. *That* is the lesson.
- **Content that must exist by this horizon** (plan across §4 / §6 / §7 / §8 first):
  - [ ] **Proficiency depth** — leaf paths reachable to a tier where compounding is actually *felt* at the Z3 band (§4 rollout).
  - [ ] **First synthesis recipe** — combines two *owned* (non-item-granted) early skills into a Z3-relevant ability (§6). Depends on which early skills exist → follows from the class secondaries (§5; the utility-secondary rule is settled in [#1398](./spikes/1398-utility-in-proficiency-system.md)).
  - [ ] **`RequiredProficiency` weapon(s)** — an uncommon weapon gated behind a leaf proficiency level: the depth route's reward (§7).
  - [ ] **A reflection source** — gear / mod / an early Retribution-path reward granting `DamageReflection`, so the reflection route can bootstrap (it's authored-only, never derived from a core attribute) (§7).
  - [ ] **Feats** — timed / under-leveled challenges on the Z3 boss, rewarding the committed builds (§8).
  - [ ] **Archetypes to validate** — at least one depth build and one breadth build that each clear the wall, proving both routes viable.
- **Resolve-first dependencies:** the class secondaries (§5; the utility-secondary rule settled in [#1398](./spikes/1398-utility-in-proficiency-system.md)) and the synthesis catalog (§6) feed this horizon directly — it firms up as those settle.

---

## Open questions / parking lot

- **Tutorials** — the arc leans on **tutorial blurbs** as a teaching lane (§2); the feature is scoped in spike **[#1392](https://github.com/ginderjeremiah/GameServer/issues/1392)** (cross-referenced there, including whether blurb content is reference data or hardcoded).
- **Classes shape starting roots, not zone access.** Early zones are class-agnostic (they provide their own counters — §5), so the class roster is *not* a hard prerequisite for the zone arc; per-class variance lives only in which paths a character *starts* training. The strawman roster is drafted in §5.
- **✓ Utility & the proficiency system — resolved** ([spike #1398](./spikes/1398-utility-in-proficiency-system.md); see §4 *Delivery archetypes & the commitment rule*). "Utility" was the wrong unit: a proficiency path is earned by a **committed** delivery archetype (opt-in enabler, measured as excess over a `0` floor), not by an effect being a buff. Universal amplifiers — including the filed CDR-buff example — get **no** path; their payoff is the paths they amplify. Attribute-concentration synergy is attribute-scaling *content*, not a track. Crit is reworked to the opt-in-multiplicative template as the worked example; the `Dexterity`/`Luck`/`Agility` identity-sourcing ripple is spun off as a follow-on spike.
- **Zone 3 build wall — synthesis and/or depth-gate.** Gateways (proficiency cross-family) are far too deep this early; the realistic Zone-3 levers are the **first skill-synthesis recipe** (opportune to introduce here) and/or **`RequiredProficiency`-gated weapons** (e.g. an uncommon weapon needing a proficiency level) — depth-gating that forces prior investment. Decide whether Z3 uses one, the other, or both.
- **Umbrella accrual vs. gating** (mechanics) — if umbrellas are capstones gated behind their leaves, the auto-firing category key would open them prematurely (see §4 principle 2). Needs a proficiency-system answer before capstone umbrellas can be authored.
- Long-horizon pacing past the 10-hour offline window — what is the **endgame** the arc is climbing toward?
