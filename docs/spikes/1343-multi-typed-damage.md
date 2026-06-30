# Spike #1343 — Multi-typed damage (multiple types per skill with split ratios)

- **Spike issue:** [#1343](https://github.com/ginderjeremiah/GameServer/issues/1343)
- **Status:** Design decided (ideation complete); implementation split into native sub-issues (see [Implementation issues](#implementation-issues)). Not yet implemented; **not** required by #1318.
- **Related:** extends the damage-types pipeline ([#1320](https://github.com/ginderjeremiah/GameServer/issues/1320)); the cross-school proficiency payoff was made forward-compatible by the effect-based accrual ([#1318](https://github.com/ginderjeremiah/GameServer/issues/1318), decision 13); composes after the mitigation rework's Toughness curve + reflection ([#1330](https://github.com/ginderjeremiah/GameServer/issues/1330)).

## Goal

Today a skill carries exactly **one** leaf `DamageType`, so a flaming sword is *either* physical *or* fire — never both. This spike lets a skill's direct hit carry **multiple types with a split** (an even split, or authored ratios): a flaming sword that is 60% physical, 40% fire. Two payoffs:

- **Cross-school skills** — a single skill that genuinely spans two (or more) damage schools, amplified and resisted per portion.
- **Cross-school masteries for free** — because the accrual already consumes per-type damage *portions*, a multi-typed hit trains each portion's proficiency path in proportion to its share. The split ratio *becomes* the cross-school contribution weight with **no proficiency-code changes** (the exact reason #1318 deliberately did not reintroduce a per-skill contribution weight).

### The framing that drove the design

> **A multi-typed hit is the single-typed hit run once per portion and summed.** One raw damage number is split by weight into portions; each portion flows through the *existing* single-type pipeline (amplify → crit → resist → Toughness) under its own type; the nets are summed. There is no new damage math and no new resistance subsystem — only a loop over the portions, and the discipline to keep that loop off the RNG stream.

Everything the single-type pipeline already does — amp/resist via the `applies()` map, vulnerability/absorption, the Toughness curve, reflection, the typed offense/incoming books — composes per portion with no change to its internals.

## Current state (what already exists)

The feature rides entirely on seams shipped by #1320/#1318/#1330.

- **A skill carries one leaf type.** `Skill.DamageType` (`Game.Core/Skills/Skill.cs`; frontend `ISkill.damageType`) is a single `EDamageType`, threaded as one argument through the whole pipeline: `BattleSkill.Update` → `BattleContext.DamageTarget(rawDamage, damageType)` → `Battler.AmplifyDamage`/`TakeDamage`/`ComputeNetDamage` (mirrored `battle-step.ts`/`battler.ts`/`battle-formulas.ts`). One type in, one type out.
- **The typed pipeline is already per-type.** `AmplifyDamage` folds `Σ applies(type).Amplification`; `ComputeNetDamage` folds `Σ applies(type).Resistance` then the Toughness curve; vulnerability (`res < 0`) and absorption (`res > 1`) fall out unclamped; reflection (`BattleContext.ReflectDamage`) is untyped and runs on the net. None of this cares *how many* types a hit has — only that it is handed one type at a time.
- **The accrual already consumes per-type portions.** `BattleContext.DamageTarget` books `Stats.AddTypedDamageDealt(type, net)` and `AddTypedDamageExposure(type, dealt)`; `BattleStats.TypedDamageDealt`/`TypedDamageExposure` are `Dictionary<EDamageType, double>`; `ProficiencyXpCalculator.Split` claims `pie × clamp(activity ÷ power)` per path off those dictionaries. **Verified:** recording each portion's net under its own type is the *only* hook multi-type needs to make cross-school proficiency work — the split ratio is the contribution weight, exactly as #1318 promised.
- **DoT is already multi-typeable.** A DoT's type is encoded by **which per-second accumulator an effect targets** (`BleedDamagePerSecond`/`PoisonDamagePerSecond`/`BurnDamagePerSecond`), and a skill may author any number of effects. So a "bleed + poison" skill is *already* expressible today by authoring two effects — no split machinery required (see decision 3).
- **The damage pipeline is parity-critical.** Unlike proficiency XP (off the tick loop), the damage math runs in the deterministic simulation on both sides, pinned by `BattleSimulatorParityTests` ↔ `battle-simulation-parity.test.ts`. The per-fire RNG draw count (`playerFires×1 + enemyFires×1`) is the strongest parity guarantee and must not change.
- **Display keys off the leaf type.** The floater/skill-tooltip read `skill.damageType` and resolve colour/icon through `damageTypeColor`/`damageTypeIcon` (`UI/src/lib/common/damage-type-display.ts`). The combat-log builder (`damage-log.ts`) phrases one type per hit. The Workbench skill editor authors `damageType` as a single native `<select>` (`UI/src/routes/admin/workbench/entities/skill.ts`).

## Decisions

Settled with the project owner during the spike:

1. **Weighted-portion split, not a blended amp/resist.** A skill carries a list of **portions**, each a `(EDamageType Type, double Weight)`. One `raw` number (`BaseDamage + Σ multipliers`) is computed once and split by weight; each portion runs the existing single-type pipeline independently and the nets are summed. Rejected: blending the types into one weighted-average amplification/resistance — amp and resist are *separate* multiplicative budgets with crit and the Toughness curve between them, so a blend is not equivalent and would lose per-portion vulnerability/absorption and the per-portion proficiency signal.

2. **Replace the single type with a portions collection.** `Skill.DamageType` is **removed**; a `SkillDamagePortion { Type, Weight }` child collection replaces it (the same EF child-table + admin child-saver pattern as `DamageMultipliers`/`Effects`). Weights are **normalized at fire time** (`portionRaw = raw × weight ÷ Σweights`, the sum folded in fixed order for parity) so authoring a portion never forces rebalancing the others; an **even split** is just equal weights (authoring sugar, not a separate mode). Pre-release, so existing skills **backfill** to a single portion `[{ Physical, 1.0 }]`. A derived `PrimaryDamageType` (the highest-weight portion, first-authored on a tie) feeds the display surfaces that still want "the skill's type" (icon/colour).

3. **Direct hits only; DoT stays per-effect.** The split applies to the **direct hit**. DoT keeps its existing model — type encoded by the targeted accumulator, multiple effects for multiple DoT types — because that is *already* multi-type-capable with no ratio machinery. This keeps the effect model untouched and adds no DoT parity surface.

4. **Portions add zero RNG draws (the load-bearing parity invariant).** The per-fire draw count stays `playerFires×1 + enemyFires×1`, independent of portion count. A **single** crit decision is rolled once per player fire and multiplies *every* portion (`crit × Σ portions = Σ (crit × portion)`, so the result is identical to multiplying the whole hit); a **single** dodge decision zeroes the *entire* hit. The portion iteration order and the float-fold of the summed net are parity contracts, exactly like the `applies()` iteration order is today.

5. **Per-portion `TakeDamage`, sum the nets, reflect once.** Each portion calls the unchanged `TakeDamage`/`ComputeNetDamage` (resistance → Toughness, per type) sequentially in fixed order; the per-hit stats accumulate. This keeps per-portion stat booking exact (each portion books precisely its own health effect) and makes the **reduce-to-single-portion identity** automatic — a one-portion skill is byte-for-byte today's single call. Reflection runs **once** on the summed net (it is linear and untyped, so per-portion vs. once are equal — once gives a single clean reflection event/log line). The per-portion absorption cap (heal clamped to `MaxHealth` room) is therefore order-dependent across portions; that is a deliberately fixed, parity-pinned behaviour, and only reachable through authored absorption (`resistance > 1`) on one of a multi-typed hit's types — a deep edge case. Rejected: sum-then-apply-once with a single cap — cleaner absorption semantics, but it muddies per-type stat reconciliation in the capped case for no real-world gain.

6. **Per-hit stats are whole-hit; typed books are per-portion.** `HighestPlayerAttack`, `CriticalDamageDealt`, and the per-skill `RecordSkillUse` total use the **summed net** of the whole multi-typed hit (one swing is one attack). `TypedDamageDealt`/`TypedDamageExposure` accumulate **per portion** under each portion's type — the cross-school proficiency signal. No `ProficiencyXpCalculator` or accrual change.

7. **Blended floater; combat log kept minimal.** The damage floater is a single number coloured by the **`PrimaryDamageType`** (so a mostly-physical hit still reads physical at a glance, like today), with a thin segmented **ratio bar** beneath encoding the exact split (scales cleanly to 3–4 portions). Crit just scales the number with the existing crit styling — the split treatment is unchanged. The combat log is **not** bent around multi-type (it is off by default and a per-portion itemization would only add noise): the existing single-line builder reports the whole hit under its primary type. The skill tooltip / Skills screen surface the portion mix.

## Damage resolution (parity-critical)

Executes **identically** on `Game.Core/Battle` and the frontend mirror, pinned by the parity matrix. `portions` is the skill's `(type, weight)` list in fixed authored order; `W = Σ weight` (folded in that order); `applies(type)` is the #1320 static map.

### Direct hits (attacker A → defender D)

```
raw    = Skill.BaseDamage + Σ_m A.attr[m.attribute] × m.amount         // base + multipliers (today)
isCrit = A is player  AND  rng.Next() < A.attr[CriticalChance]         // ONE draw, player only
isDodge= A is enemy   AND  rng.Next() < D.attr[DodgeChance]            // ONE draw, enemy→player only

if isDodge:                                                            // dodge zeroes the WHOLE hit
    DamageDodged += Σ_i D.ComputeNetDamage(A.Amplify(raw×w_i/W, t_i), t_i, A.Level)
    return 0                                                           // no reflection on a dodge

totalNet = 0
for (t_i, w_i) in portions:                                           // fixed order (parity)
    dealt_i = A.Amplify(raw × w_i / W, t_i)                           // attacker amp for this portion's type
    if isCrit:  dealt_i ×= A.attr[CriticalDamage]                     // single crit multiplies every portion
    net_i   = D.TakeDamage(dealt_i, t_i, A.Level)                     // resist → Toughness (today's call), per type
    book typed: dealt book += net_i under t_i;  exposure book += (pre-resist dealt_i) under t_i
    totalNet += net_i

book whole-hit stats from totalNet (HighestPlayerAttack, CriticalDamageDealt, RecordSkillUse)
ReflectDamage(totalNet)                                               // once, untyped, on the net (linear)
return totalNet
```

- **Crit/dodge are single decisions** (decision 4): the draw count is unchanged, and a crit multiplies all portions while a dodge zeroes all of them.
- **Per-portion `TakeDamage`** (decision 5) reuses the unchanged resistance → Toughness pipeline, so vulnerability/absorption and the curve are per-portion for free; the per-portion absorption cap is order-dependent (fixed order ⇒ parity-safe).
- **Reduce-to-single-portion identity:** with one portion at weight `w`, `raw × w/W = raw`, the loop runs once, `totalNet` is that single `TakeDamage`, and the whole path is byte-for-byte today's single-type hit — so the existing parity matrix is undisturbed and only multi-typed content changes outcomes.

### DoT / HoT

**Unchanged** (decision 3). The end-of-tick typed-DoT phase already loops the per-type accumulators; multi-typed DoT is authored as multiple effects today. No change.

## Changes by area

### A. Data model + authoring — `SkillDamagePortion`, backfill, Workbench editor
Remove `Skill.DamageType`; add the `SkillDamagePortion { Type, Weight }` child collection (entity, contract, mapper, DbContext + migration, **backfill every skill to `[{ Physical, 1.0 }]`**); add the derived `PrimaryDamageType` accessor for display; codegen the contract to the client. Replace the skill editor's single damage-type `<select>` with a **portions table** (add/remove rows, per-row type + weight, an "even split" convenience that equalizes weights) plus validation (≥1 portion, positive weights). **No battle-behaviour change** until B reads it. Foundation; no external dependency.

### B. Direct-hit multi-type pipeline + parity
Implement the portion loop (decisions 4–6) in `BattleContext.DamageTarget` and the frontend `battleStep`, threading the skill's portions instead of a single type; keep the single crit/dodge decision, per-portion `TakeDamage`, summed-net reflection, and per-portion typed-book recording. New mirrored parity vectors: a 60/40 hit, resist-one-type-not-the-other, per-portion vulnerability and absorption, crit-applies-to-all-portions, dodge-zeroes-all, and the reduce-to-single-portion identity. Depends on **A**. The core of the spike.

### C. Floater + skill-tooltip UX
The blended floater (decision 7): `PrimaryDamageType` colour for the number, a segmented ratio bar beneath for the split, crit unchanged. Surface the portion mix on the skill tooltip / Skills screen via the same `PrimaryDamageType` + portion list. Frontend-only. Depends on **A**, **B**.

### D. Example multi-typed content + cross-school verification
Author a couple of multi-typed skills (a flaming sword, a storm blade) and a relevant enemy or two to exercise the system end-to-end, and verify the cross-school proficiency training falls out (each portion trains its path in proportion). Enemies field the same skills, so multi-typed *enemy* combat comes free once **A**/**B** land. Depends on **A**, **B**, **C**.

## Implementation issues

Created as native sub-issues of #1343:

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| [#1384](https://github.com/ginderjeremiah/GameServer/issues/1384) — Data model + authoring (`SkillDamagePortion`, backfill, Workbench portions editor) | A | — | medium |
| [#1385](https://github.com/ginderjeremiah/GameServer/issues/1385) — Direct-hit multi-type pipeline + parity | B | A | medium |
| [#1386](https://github.com/ginderjeremiah/GameServer/issues/1386) — Blended floater + skill-tooltip portion mix | C | A, B | small |
| [#1387](https://github.com/ginderjeremiah/GameServer/issues/1387) — Example multi-typed content + cross-school verification | D | A, B, C | small |

**A** is the foundation (inert data + authoring). **B** is the parity-critical core and adds the only frontend↔backend parity vectors. **C** and **D** are the player-facing surface once **B** exists. The rest carry unit/integration tests.

## Documentation to update on landing

- `docs/game-design.md` — the **Damage types** section: a skill's direct hit may carry **multiple leaf types with a split**, amplified/resisted per portion, and the split ratio is the cross-school proficiency contribution weight. Note DoT remains per-effect. (With **A**, **B**.)
- `docs/backend-battle.md` — the direct-hit section: the per-portion loop, the single crit/dodge decision (draw count unchanged), summed-net reflection, and the reduce-to-single-portion identity. (With **B**.)

## Out of scope / deferred

- **Multi-typed DoT split ratios** — DoT is already multi-typeable via multiple effects (decision 3); ratio-splitting a single effect across accumulators is a possible later uniformity nicety, not worth the effect-model rework now.
- **Type conversion** (imbue weapon with fire; "your physical hits deal fire") — *synergistic*: with portions, conversion becomes a portion reweight/replace at fire time rather than a single-type swap. Multi-type makes it easier to add later; still its own follow-up (deferred from #1320).
- **Penetration** (ignore X% of a type's resistance) — orthogonal; applies per portion (subtract from that portion's `resTotal`). Multi-type does not change it (deferred from #1320).
- **Per-type / per-portion statistics & challenges** ("deal X fire damage with a hybrid skill") — the per-portion type is present at the recording site, so these are cheap when a use case lands.
- **Per-type independent base/multipliers** (a skill computing a *separate* damage number per type, rather than splitting one) — a much larger feature (effectively multiple skills in one); the split model deliberately computes `raw` once.
