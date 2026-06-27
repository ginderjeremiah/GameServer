# Spike #1320 — Damage types for skills & effects

- **Spike issue:** [#1320](https://github.com/ginderjeremiah/GameServer/issues/1320)
- **Status:** Design decided; split into implementation sub-issues (see [Implementation issues](#implementation-issues)). Not yet implemented.
- **Related:** motivated by the proficiency system ([#982](https://github.com/ginderjeremiah/GameServer/issues/982)) — proficiencies need to buff *specific* skills, not every skill that scales off a core attribute. Builds on the attribute graph ([#528](https://github.com/ginderjeremiah/GameServer/issues/528)), player crit/dodge/block ([#178](https://github.com/ginderjeremiah/GameServer/issues/178), the decimal-percentage convention this reuses), and skill effects ([#180](https://github.com/ginderjeremiah/GameServer/issues/180), the timed-effect / DoT substrate this types).

## Goal

Today damage is an undifferentiated number: 10 from a fireball and 10 from a sword are identical to every system that touches them. The only way to make a skill stronger is to buff the **core attribute it scales off** (Intellect, Strength, …), which spills onto *every* skill that scales the same way — so a "Fire Magic" proficiency can't strengthen fire without strengthening all magic.

Introduce **damage types** so that damage carries a discriminator (fire / water / earth / wind / physical, plus the damage-over-time forms bleed / poison / burn). This unlocks:

- **Targeted amplification** — a proficiency, item, mod, class passive, or skill-effect can buff *one* type (the primary driver).
- **Targeted resistance** — enemies (and players) can resist or be vulnerable to a type, adding build/encounter strategy (the secondary payoff).

Both sides — **amplification on the attacker and resistance on the defender** — are in V1, for both the player→enemy and enemy→player directions.

### The framing that drove the design

The whole game already routes every combat bonus (gear, mods, skill-effects, proficiency payouts, class passives, crit/dodge/block) through **one attribute-modifier pipeline**. So damage types are modelled to ride that pipeline rather than as a parallel resistance subsystem:

> **A damage type is a tag on the damage; the bonus and the resistance are just more attributes.** A skill carries one leaf type; per-type/per-category amplification and resistance are percentage attributes resolved through a static map. Nothing needs a new way to "grant a fire bonus" — proficiencies, items, mods, effects, and class passives all gain typed buffs for free through the modifier path they already use.

The single piece of new machinery is the **lookup indirection**: the damage math resolves "which amp/resist attributes apply to this hit" through a map, never a hardcoded 1:1 type↔attribute pair. With that loop in place, cross-cutting categories, true/typeless damage, penetration, and (later) multi-type or type-conversion become cheap additions instead of refactors.

## Current state (what already exists)

The feature rides on seams already in place.

- **Damage is a single undiscriminated number.** A skill deals `BaseDamage + Σ(attribute × multiplier)` (`Game.Core/Skills/Skill.cs`, `Game.Core/Battle/BattleSkill.cs:CalculateDamage`; frontend mirror `UI/src/lib/battle/battle-formulas.ts:calculateSkillDamage`). `Skill` carries `BaseDamage`, `DamageMultipliers`, `Effects`, `CooldownMs`, `Rarity` — **no type field**.
- **The damage pipeline is flat-Defense only.** After raw damage (and the #178 crit multiply, player-only, pre-Defense), the defender's `TakeDamage` subtracts flat `Defense` (+ `BlockReduction` on a block), clamped at `0` (`Game.Core/Battle/Battler.cs:TakeDamage`; frontend `applyDefense`). There is **no multiplicative mitigation step** — the natural home for percentage resistance.
- **The attribute graph composes new contributors for free, and scales.** `AttributeCollection` (backend) / `BattleAttributes` (frontend) store values in a **dense array indexed by the enum with lazy per-attribute nodes**; reads are O(1) cached, and totals recompute **only when a modifier is added/removed** (effect apply/expire) — never per tick, and only over the touched attribute's small modifier list. Derived attributes and `StaticAttributeModifiers` work off **fixed hardcoded subsets** (the 6 core, ~9 derived), independent of enum size. **Adding ~20+ damage-type attributes adds essentially zero runtime cost to a live battle.**
- **Skill effects are timed attribute modifiers.** `SkillEffect` (`Game.Core/Skills/SkillEffect.cs`) carries `Target` (Self/Opponent), `AttributeId`, `ModifierType`, `Amount`, `DurationMs`, `ScalingAttributeId`, `ScalingAmount`. DoT/HoT are realized as effects on two **per-second** attributes consumed by an end-of-tick phase.
- **DoT is collapsed into a single `DamageTakenPerSecond` attribute** — an MVP shortcut from #180, never the intended end state. There is no per-type or per-source attribution. Typing DoT *completes* this rather than reworking a deliberate decision.
- **Enemy attributes are a sparse list of distributions.** `Enemy.AttributeDistributions` is a list of `(AttributeId, BaseAmount, AmountPerLevel)` resolved at the enemy's level (`Game.Core/Enemies/Enemy.cs`). An enemy can carry any number of per-type resistances as extra distribution rows — **no model change needed**, only authoring/tooling.
- **The decimal-percentage convention exists.** Percentage attributes are stored as decimal fractions (`0.05` = 5%) and consumed without transformation (#178 / #528). The new amplification/resistance attributes adopt this convention exactly.
- **The admin attribute picker is a flat native `<select>` of every enum value**, used in 5 authoring contexts (skill multipliers, skill effects, item mods, enemy distributions, class stat locks; `UI/src/routes/admin/workbench/.../reference.svelte.ts:attributeOptions`, `FieldControl.svelte`). At ~17 options it is fine; at the scale this spike (and future content) pushes the enum to, it becomes unusable and needs search + grouping.
- **Enums and `StaticAttributeModifiers` codegen to the client**, so the frontend mirror of any added enum value / static row is generated, keeping the two simulators in lockstep.

## Decisions

Settled with the project owner during the spike:

1. **Attribute-based, not a parallel subsystem.** A skill carries a leaf `DamageType`; per-type and per-category **amplification** (attacker) and **resistance** (defender) are percentage attributes (`EAttribute`), resolved through a static map. Every existing modifier source (proficiency, item, mod, skill-effect, class passive) can grant them with no new plumbing.

2. **Leaf type + a static `type → keys` map.** Each damage instance has exactly **one** leaf type. A static map resolves a type to the set of attribute **keys** whose amp/resist apply to it — the leaf type itself plus any **cross-cutting categories** it belongs to. The damage math iterates this set; it never hardcodes a single type↔attribute pair. This indirection is the one piece of machinery that must exist up front.

3. **Taxonomy: eight leaf types, two cross-cutting categories.** Leaf types: `Physical`, `Fire`, `Water`, `Earth`, `Wind` (direct), `Bleed`, `Poison`, `Burn` (DoT). Cross-cutting categories (V1): **`Elemental`** (fire/water/earth/wind, **not** physical) and **`DoT`** (bleed/poison/burn). `Physical` is a leaf type but **not** cross-cutting; there is **no** `Magical` category (too broad). Cross-cutting category membership per type:

   | Leaf type | Applicable keys |
   | --- | --- |
   | Physical | `Physical` |
   | Fire | `Fire`, `Elemental` |
   | Water | `Water`, `Elemental` |
   | Earth | `Earth`, `Elemental` |
   | Wind | `Wind`, `Elemental` |
   | Bleed | `Bleed`, `DoT` |
   | Poison | `Poison`, `DoT` |
   | Burn | `Burn`, `Fire`, `Elemental`, `DoT` |

   `Burn` pulls `Fire` + `Elemental` + `DoT`, so a fire proficiency or a fire-immune enemy affects burns for free. `Bleed`/`Poison` are self + `DoT` only (adding `Physical` to `Bleed`'s row is the one-line change if bleed-resisted-by-physical is wanted later — deliberately left out for now).

4. **Resistances and amplifications are additive within a side, applied as separate multipliers across sides.** For a hit, `ampTotal = Σ applicable amplification attributes` (read from the attacker) and `resTotal = Σ applicable resistance attributes` (read from the defender). They apply as `× (1 + ampTotal)` and `× (1 − resTotal)` — **separate budgets**, not a single `(1 + amp − res)` (resistance must not cancel amplification 1:1). Stored in the decimal-percentage convention (`0.30` = 30%).

5. **Left unclamped — vulnerability and absorption fall out for free.** `resTotal < 0` ⇒ factor > 1 ⇒ the target **takes extra** (vulnerability). `resTotal > 1` ⇒ factor < 0 ⇒ a **net heal** (absorption). No special machinery and no restriction on either — flat Defense is structured so it never heals and never amplifies absorption (see [Damage resolution](#damage-resolution-parity-critical)).

6. **% resistance first, flat Defense last.** The percentage mitigation applies to the full typed hit; flat `Defense`/`BlockReduction` chip the remainder last (the project owner's stated preference). Crit stays in the attacker-side damage, so it still **punches through** Defense. (This ordering, and DoT bypassing Defense entirely, are the balance levers — see the flat-reduction rework in [Out of scope](#out-of-scope--deferred).)

7. **DoT is typed via per-type per-second accumulators.** `DamageTakenPerSecond` is replaced by `BleedDamagePerSecond` / `PoisonDamagePerSecond` / `BurnDamagePerSecond`. The DoT type is encoded by **which accumulator an effect targets** — no separate field on `SkillEffect`. The end-of-tick phase loops the DoT types, applying each type's resistance. **Caster amplification is frozen into the accumulator at apply time** (consistent with how effect magnitude / caster-scaling already freezes); **defender resistance is sampled live per tick** (so a vulnerability debuff makes existing burns hurt immediately). DoT keeps bypassing flat Defense, so resistance is its **only** mitigation. HoT (`HealthRegenPerSecond`) stays **typeless**.

8. **Bidirectional, both sides, but crit/dodge/block stay player-only.** Player and enemy skills are both typed; both battlers amplify and resist (enemies via `AttributeDistribution` rows). The #178 player-only asymmetry for crit/dodge/block is unchanged — amplification/resistance are a separate, symmetric mechanic.

9. **Existing content backfills to a sane default.** Pre-release, so no grandfathering: existing skills backfill to `Physical`; existing `DamageTakenPerSecond` effects re-home to the appropriate typed accumulator (a content decision per effect). The new attributes default to `0` from every source.

## Attribute additions

Each **key** in the map (`Physical`, `Fire`, `Water`, `Earth`, `Wind`, `Bleed`, `Poison`, `Burn`, `Elemental`, `DoT`) backs two attributes — `…Amplification` and `…Resistance` — for **20** new percentage attributes, plus **3** per-type DoT accumulators replacing the single `DamageTakenPerSecond`. All adopt the decimal-percentage convention and render scaled ×100; amplification/resistance default to `0` (no base), so an untyped or unmodified battler reads `0` everywhere.

The enum will keep growing as content authoring ramps (200+ values is expected and architecturally fine, per the storage analysis above). The cost of that growth lands on **authoring UX and breakdown display**, not the runtime — hence the picker upgrade and the by-type grouping below.

## Damage resolution (parity-critical)

Everything here executes **identically** on `Game.Core/Battle` and its frontend mirror, and is pinned by the mirrored parity matrix (`BattleSimulatorParityTests` ↔ `battle-simulation-parity.test.ts`). `applies(type)` is the static map from decision 3; the iteration order over its keys is fixed for float parity.

### Direct hits (attacker A → defender D)

```
1. dealt  = Skill.BaseDamage + Σ_m  A.attr[m.attribute] × m.amount        // base + multipliers (today)
2. amp    = Σ_{k ∈ applies(Skill.DamageType)}  A.attr[kAmplification]      // attacker, additive
   dealt *= (1 + amp)
3. if A is player and crit roll hits:  dealt *= A.attr[CriticalDamage]     // player-only, before mitigation
4. if D is player and dodge roll hits:  net = 0;  done                     // player-only
5. res    = Σ_{k ∈ applies(Skill.DamageType)}  D.attr[kResistance]         // defender, additive
   mitigated = dealt × (1 − res)                                           // UNCLAMPED
6. if mitigated > 0:
       flat = D.attr[Defense] + (D is player and block roll hits ? D.attr[BlockReduction] : 0)
       net  = max(mitigated − flat, 0)                                     // identical to today's flat step
   else:
       net  = mitigated                                                    // absorption heal; flat N/A
7. D.health -= net          // net < 0 heals
```

- **amp / res are separate multipliers** (decision 4); crit (step 3) is attacker-side and still lands before mitigation, so it punches through Defense.
- **Vulnerability and absorption** (decision 5): a negative `res` amplifies; a `res > 1` drives `mitigated < 0`, which step 6 routes to a heal that flat Defense never touches ("Defense can't heal you" still holds).
- **Crit/dodge/block** remain gated on the player (decision 8); the RNG draw order from #178 is unchanged (amp/resist consume no draws — they are deterministic).

### DoT / HoT phase (end of tick, both battlers alive, after both skills resolve)

```
for each battler B  (enemy first, then player):
    dot = 0
    for t in [Bleed, Poison, Burn]:                    // fixed order for parity
        perSec = B.attr[t.PerSecond]
        if perSec == 0:  continue
        res = Σ_{k ∈ applies(t)}  B.attr[kResistance]   // B is its own DoT's defender; sampled LIVE
        dot += (perSec × MsPerTick / 1000) × (1 − res)
    B.health -= dot                                     // bypasses Defense; NOT floored (vuln/absorb natural)
    B.health  = min(B.health + B.attr[HealthRegenPerSecond] × MsPerTick/1000, MaxHealth)   // HoT, typeless
    check death for B
```

- DoT **bypasses flat Defense** — resistance is its only mitigation, which is what makes DoT-resist worth investing in.
- **Amp frozen, resist live** (decision 7): caster amplification is baked into the per-second magnitude at apply time; defender resistance is sampled per tick.
- Absorption/vulnerability work with no extra code (DoT was never floored). Enemy-first ordering and the damage→heal→death sequence are unchanged (#1090).

### Reduce-to-today guarantee

With no typed content present, `amp` and `res` are `0.0`, so steps 2 and 5 multiply by `1.0` (exact in IEEE-754) and step 6's positive branch is byte-for-byte today's `max(raw − Defense − BlockReduction, 0)`. Untyped skills and the existing parity matrix are therefore **undisturbed** — only authored types change outcomes. Both ports must still fold the additive sums identically (the usual parity discipline) and iterate `applies()` in the same fixed order.

## Changes by area

### A. Foundation — `EDamageType`, the amp/resist attributes, and the `applies()` map
`EDamageType` enum (8 leaf types); the 20 `…Amplification`/`…Resistance` `EAttribute` values (decimal-percentage convention, base `0`); the static `applies(type) → keys` map and the per-hit lookup helper, **on both sides** (codegen the map/table to the client like `StaticAttributeModifiers`); display metadata (`IsPercentage`, codes) for the new attributes, grouped by damage type for the breakdown screen. **No battle-behaviour change** — the attributes are inert until B/C read them (the #178 foundation pattern). Foundation; no external dependency.

### B. Direct-hit typing — the damage pipeline
`Skill.DamageType` (entity, contract, mapper, DbContext + migration, admin skill-editor field, **default/backfill `Physical`**). Implement the direct-hit algorithm (amp on the attacker, resist on the defender, the resist-then-flat-Defense order with the absorption-safe branch) in `BattleContext`/`BattleSkill` and the frontend mirror. Bidirectional (player and enemy skills typed; both amplify and resist). New parity vectors: forced amp, forced resist, vulnerability (`res < 0`), absorption (`res > 1`), crit-still-punches-through, and the reduce-to-today identity (zero amp/resist ⇒ unchanged outcome). Depends on **A**.

### C. DoT split — typed damage-over-time
Replace `DamageTakenPerSecond` with `BleedDamagePerSecond`/`PoisonDamagePerSecond`/`BurnDamagePerSecond`; re-home existing DoT effects to the appropriate accumulator (content backfill). Rework the end-of-tick phase to loop the DoT types and apply each type's resistance (live), freezing caster amplification into the accumulator at apply time. Preserve bypass-Defense, not-floored, enemy-first, and damage→heal→death. New parity vectors covering typed DoT, DoT resistance, DoT vulnerability/absorption, and the reduce-to-today identity. Depends on **A**; coordinates with **B** on the shared `applies()` helper.

### D. Enemy authoring — typed enemy combat + resistances
Enemy skills are typed automatically once `Skill.DamageType` exists (enemies field the same skills). The enemy-specific work: author **resistance (and amplification) distributions** on enemies through the enemy editor (the `AttributeDistribution` model already supports it), plus a handful of example resistant/vulnerable enemies to exercise the system end-to-end. Depends on **A**, **B**, **C** (and benefits from **E**).

### E. Searchable attribute picker (Workbench)
Upgrade the flat native `<select>` attribute picker to a searchable, **group-by-type** control, since 5 authoring contexts hit it and the enum is now large. Self-contained tooling improvement; valuable as soon as **A** lands. Soft-depends on **A** (the new attributes give it something to group).

### F. UX & combat log
Typed, colour/icon-tagged damage numbers; resist / vulnerable / absorbed feedback in the combat log (extend the `Damage`/`SkillEffect` log types); the attribute-breakdown screen **grouped by damage type** so the larger attribute set stays readable. Frontend-only. Depends on **B**, **C**.

## Implementation issues

Created as native sub-issues of #1320:

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| Foundation — `EDamageType`, amp/resist attributes, `applies()` map | A | — | medium |
| Direct-hit typing — `Skill.DamageType` + damage pipeline + parity | B | A | medium |
| DoT split — per-type accumulators + typed DoT phase + parity | C | A | medium |
| Enemy authoring — typed enemy combat + resistance distributions | D | A, B, C | small |
| Searchable attribute picker (Workbench) | E | A (soft) | small |
| UX & combat log — typed damage, resist feedback, grouped breakdown | F | B, C | medium |

**A** is the foundation (inert data). **B** and **C** are the parity-critical core and can run in parallel after **A**. **E** can land any time after **A** and unblocks comfortable authoring. **D** and **F** are the content/player-facing surface once **B**/**C** exist. **B** and **C** each add **frontend↔backend parity** vectors; the rest carry unit/integration tests.

## Documentation to update on landing

- `docs/game-design.md` — a **Damage types** section: the taxonomy + categories, additive amp/resist, vulnerability/absorption, typed DoT, and how proficiencies/items/effects target a type. (With **A**–**D**, **F**.)
- `docs/backend-battle.md` — the damage-resolution order (% resist then flat Defense; crit punch-through), the typed DoT phase (amp-frozen/resist-live, bypass-Defense), and the new parity surface. (With **B**, **C**.)
- `docs/backend.md` / `docs/frontend.md` — the `applies()` map as a codegen'd shared table, and the attribute-breakdown grouping, if a cross-cutting mention is warranted. (With **A**, **F**.)

## Out of scope / deferred

- **Flat-mitigation (Defense & Block) rework** — split out as its own spike, [#1330](https://github.com/ginderjeremiah/GameServer/issues/1330) (`docs/spikes/1330-mitigation-rework.md`): Defense becomes a diminishing-curve %, and Block is removed (redundant with Defense, and RNG defense is anti-tank) with its slot repurposed into deterministic, authored-only **damage reflection** (a real tank kill-condition in a game that otherwise draws on the timeout). It is a **localized swap of this pipeline's final mitigation step** — it does not touch the `applies()` map or the type-resist math — so V1 here keeps flat Defense/Block as-is and the two land independently. (When it lands, step 6's absorption branch disappears, since no flat subtraction remains to floor.)
- **Type conversion** — a mod/effect that changes a skill's resolved type (imbue weapon with fire; "your physical hits deal fire"). High-interest follow-up; the single-type model leaves room (conversion swaps the type at fire time). Own issue when picked up.
- **Penetration** — an amp-side "ignore X% of a type's resistance" knob. Trivial under the additive model (subtract from `resTotal`); reserve the slot, defer the feature.
- **True / typeless damage** — a leaf option that ignores resistance (chip damage, the path-less "punch" fallback). Free under the map; add when a use case lands.
- **Per-type statistics & challenges** — "deal X fire damage", "win without taking poison". Cheap *if the type is present at the damage-recording site*, which B/C ensure; the per-type breakdown rows are the deferred part.
- **Multi-type / split-damage skills** (a flaming sword that is part fire, part physical) and **elemental reactions/combos** (wet + fire) — the model is built not to forbid them, but both are out of V1.
