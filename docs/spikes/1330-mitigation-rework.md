# Spike #1330 — Flat-mitigation rework (Defense armor curve + Block → reflection)

- **Spike issue:** [#1330](https://github.com/ginderjeremiah/GameServer/issues/1330)
- **Status:** Design decided; implementation underway. **Areas A (#1332) and B (#1333) are implemented** — `Defense` is renamed to `Toughness` and applied as the diminishing curve `Toughness / (Toughness + K·attackerLevel)` (Endurance-only derivation, `K` = `GameConstants.ToughnessMitigationConstant`), and **Block has been retired and replaced by deterministic, authored-only `DamageReflection`** (the enemy attack now draws dodge only; the direct-hit stack is fully multiplicative with the flat floor and its absorption-safe branch gone). The UI/reflection authoring surface (Area C/#1334) is still to follow.
- **Related:** surfaced during the damage-types spike ([#1320](https://github.com/ginderjeremiah/GameServer/issues/1320)) — this reworks the **final mitigation step** of that spike's damage-resolution pipeline. Deferred defensive mechanics live in [#1331](https://github.com/ginderjeremiah/GameServer/issues/1331); ramping "fortify" as a class passive is noted in [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219).

## Goal

Replace **flat damage reduction** with mitigation that scales sensibly, and give the tank archetype a mechanic that fills a real need rather than a second, randomized copy of Defense.

Flat subtraction (`Defense`, and Block's flat `BlockReduction`) is **scale-dependent**: its value is entirely `reduction ÷ incoming-hit-size`, so a fixed investment is overpowering against small hits and worthless against big ones, with a degenerate immunity breakpoint at the `0` floor. That is the "useless or way too powerful" feel. **Block** compounds it: it gates flat reduction behind RNG (anti-tank — tanks want *low* variance), routinely zeroes small hits (silently becoming a dodge), and overlaps both `Defense` (same reduce-damage operation) and `Dodge` (the existing high-variance defensive / evasion niche). Block isn't pulling its weight as a distinct mechanic.

## Current state (what already exists)

- **Mitigation is flat-only.** After raw damage (and the #178 crit multiply, player-only, pre-Defense), the defender's `TakeDamage` subtracts flat `Defense` (+ `BlockReduction` on a successful block) in one clamp `max(raw − Defense − BlockReduction, 0)` (`Game.Core/Battle/Battler.cs:TakeDamage`; frontend `applyDefense`). There is no multiplicative mitigation step.
- **Defense / block sources.** `Defense` derives from Endurance/Agility via `StaticAttributeModifiers`; `BlockChance` (decimal prob, from Endurance) gates a `BlockReduction` (base `2` + `0.5·Endurance`). All player-only for the roll (#178).
- **Crit/dodge/block are player-only seeded rolls** with a fixed RNG draw order (#178): each enemy attack draws dodge then block. Removing block removes one draw from that order.
- **The attribute graph composes new contributors for free** and is parity-mirrored (`AttributeCollection` ↔ `BattleAttributes`); the damage pipeline is pinned by `BattleSimulatorParityTests` ↔ `battle-simulation-parity.test.ts`.
- **Damage types (#1320)** add a multiplicative `(1 − Σ typeResist)` step before flat Defense, plus an absorption-safe branch around the flat floor. This spike removes the flat step those work around.

## Decisions

Settled with the project owner during the spike:

1. **Defense becomes a diminishing-curve percentage.** Convert the bulk stat (`Armor`) to `defReduction = Armor / (Armor + K·attackerLevel)` and apply it as a **multiplier**, not a subtraction. The key property: **effective HP is linear in Armor** (each point is worth a constant % EHP — never useless) even though the % reduction has diminishing returns; the % asymptotes below 100% (never immune, no breakpoint). `K` scales on the **attacker's** level so Armor stays relevant relative to who is hitting you, fixing the high-zone "useless" cliff. Applies to **both** battlers.

2. **Remove Block as damage reduction.** It is redundant with Defense and RNG defense is anti-tank. Retire `BlockChance` and `BlockReduction`. `Dodge` remains as the player-only high-variance evasion proc (Agility's niche); `Crit` remains the offensive proc — so the defensive RNG niche is fully owned by Dodge, with no overlap.

3. **Repurpose the slot into deterministic damage reflection.** A `DamageReflection` percentage read on the **defender**: on a **direct hit**, after mitigation, deal `net × DamageReflection` to the **attacker**. It is:
   - **Deterministic** (no proc) — tanks want reliability, and it removes block's RNG draw, *simplifying* the #178 stream.
   - **Authored-only** — granted by gear / proficiency / class, **not** derived from a core attribute. So it is a deliberate **build identity with opportunity cost**, not a stat tax, and player/enemy gating is moot: an enemy has reflection only where a designer authors it (a free boss-design seam — "don't out-DPS the spikes").
   - **A real kill condition.** A pure tank otherwise just **draws** on the 2-minute timeout; reflection converts incoming damage into a win path, and **self-scales** (strongest exactly against the hard-hitters you tank).
   - **Scoped to direct hits** (no coherent attacker for a DoT tick), and **bypasses the attacker's own mitigation** (otherwise it double-dips to nothing).

   *Retaliation* (a counter scaling off your own stats, tied to enemy attack frequency rather than hit size) was the considered alternative; reflection chosen for its self-scaling elegance and single-knob simplicity.

4. **The whole mitigation stack becomes multiplicative.** Combined with damage types (#1320), the defender's mitigation is:

   ```
   net = dealt
       × (1 − Σ typeResist)                   // additive resist budget; >1 ⇒ absorb (heal)   — both sides
       × (1 − Armor/(Armor + K·attackerLvl))  // armor curve; asymptotes <100%, linear EHP    — both sides
   then, on a direct hit:  attacker.health −= net × defender.DamageReflection   // authored-only
   ```

   With no flat subtraction anywhere, the damage-types **absorption branch disappears** (the only path to negative `net` is `Σ typeResist > 1`, the intended absorption heal); no stat trivially reaches immunity (Armor asymptotes; only authored type-resist can hit 100%); and the archetypes separate cleanly — **Agility → Dodge** (variance evasion), **Endurance → Armor curve** (smooth bulk) + authored **Reflection** (offense-through-defense), **Dex/Luck → Crit** (offensive variance).

## The curve (strawman — tunable)

`defReduction = Armor / (Armor + K·attackerLevel)`, with `Armor` sourced from Endurance (+ gear/mods) via `StaticAttributeModifiers` and `K` a global constant tuned so a level-appropriate Armor investment sits in a sensible band (e.g. 30–60%). `K·attackerLevel` keeps the band stable as both Armor and enemy damage grow with content. `DamageReflection` magnitude, the Endurance→Armor coefficient, and `K` are all strawman values to balance during implementation (the #178/#528 convention). Reflection is bounded by incoming damage; the main tuning risk is an over-tuned reflect against a glass-cannon enemy.

## Changes by area

### A. Defense → armor curve
Replace the flat `Defense` subtraction with the curve multiplier on both sides (`Battler.TakeDamage` ↔ `applyDefense`), threading the **attacker's level** into the mitigation call. Rework the `Defense`/`Armor` derivation in `StaticAttributeModifiers` (mirrors to the client via codegen). Re-author the parity scenarios that assert flat-Defense outcomes. Parity-critical.

### B. Remove Block; add deterministic reflection
Retire `BlockChance`/`BlockReduction` (enum entries, derivations, `TakeDamage` block param, `SkillActivation.blocked` flag, combat-log block line) and **remove the block draw** from the #178 RNG order (dodge-only on enemy attacks now). Add the authored-only `DamageReflection` attribute and apply it post-mitigation on direct hits, dealing to the attacker (bypassing the attacker's mitigation), on both sides. New parity vectors: the curve, reflection on a direct hit, reflection ignores DoT, and the simplified draw order (enemy attack = 1 dodge draw). Parity-critical.

### C. UI / combat log + authoring
Combat-log line for reflected damage; drop the block line. Surface `Armor`/`DamageReflection` on the attribute-breakdown screen (block stats removed). Verify gear/mod/proficiency/class authoring can grant `DamageReflection` (authored-only, so it needs an authoring path but no core-attribute derivation).

## Implementation issues

Created as native sub-issues of #1330:

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| [#1332](https://github.com/ginderjeremiah/GameServer/issues/1332) — Defense → diminishing armor curve | A | — | medium |
| [#1333](https://github.com/ginderjeremiah/GameServer/issues/1333) — Remove Block, add deterministic damage reflection | B | A | medium |
| [#1334](https://github.com/ginderjeremiah/GameServer/issues/1334) — UI / combat log + reflection authoring | C | A, B | small |

**A** and **B** are the parity-critical core (both touch the shared mitigation path, so **B** sequences after **A**); **C** is the player-facing surface. Each carries unit/integration tests; **A** and **B** add **frontend↔backend parity** vectors. Whichever of this spike and the damage-types direct-hit work (#1324) lands second removes the flat-floor absorption branch (the two are otherwise independent).

## Documentation to update on landing

- `docs/game-design.md` — the **Critical hits, dodge & block** section: Defense as an armor-curve %, Block removed, deterministic authored **reflection** as the tank kill-condition, and the refreshed archetype mapping.
- `docs/backend-battle.md` — the damage-resolution order (now fully multiplicative, flat floor gone), the simplified #178 draw order (dodge-only on enemy attacks), and reflection as a parity-critical mechanic.

## Out of scope / deferred

- **Other defensive mechanics** — mitigation→sustain, absorb shield, last-stand — tracked in [#1331](https://github.com/ginderjeremiah/GameServer/issues/1331) (shield/last-stand lean toward the skill-effect system; mitigation→sustain toward an authored attribute like reflection).
- **Ramping "fortify"** (tankier the longer the fight) — a candidate **class passive**, noted in [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219); deepens attrition without a kill condition, so it pairs with reflection rather than replacing it.
- **Anti-burst damage cap** (no hit exceeds X% of MaxHealth) — **not pursued for now**: deliberately avoiding a stat that exists only to counter specific enemies, especially one keyed to a single player archetype. Revisit only if a genuine burst meta emerges.
- **Enemy reflection / retaliation as a boss mechanic** — the authored-only model makes it free to add later; out of this V1.
