# Spike #1426 — Attribute identity-sourcing: what DEX/LUK/AGI do when combat identities are opt-in

- **Spike issue:** [#1426](https://github.com/ginderjeremiah/GameServer/issues/1426)
- **Status:** Ideation complete; design decided. Implementation split into sub-issues (see [Implementation issues](#implementation-issues)); one follow-on spike opened ([combat-rating power measure](https://github.com/ginderjeremiah/GameServer/issues/1526)).
- **Related:** follow-on from [#1398](./1398-utility-in-proficiency-system.md) (Decision 8); builds on the crit rework ([#1425](https://github.com/ginderjeremiah/GameServer/issues/1425) / [#1453](https://github.com/ginderjeremiah/GameServer/issues/1453)), the parry template ([#1457](https://github.com/ginderjeremiah/GameServer/issues/1457)), and the share-claim tally ([#1481](https://github.com/ginderjeremiah/GameServer/issues/1481)).

## The question, as filed

Making combat identities opt-in (crit via #1425, prospectively CDR and dodge) empties `Dexterity`/`Luck`/`Agility` of the derived stats they source. Per stat: multiplicative-amplifier vs direct removal; whether the three remain allocatable attributes at all (the newbie-trap concern); and the Frequency/CDR path that becomes reachable iff CDR is severed.

## What had shifted by the time the spike ran

Three landings reshaped the question as filed:

1. **Crit took a different shape than the seed direction** (#1425 + #1453): a **per-skill** authored `CriticalChance` (default `0`) × a base-`1` `CriticalChanceMultiplier` — not a flat item-sourced chance attribute. The DEX/LUK → chance derivations were already gone; LUK kept crit damage (`1.5 + 0.0025·Luck`). The only open crit question left was whether DEX gets a multiplier derivation.
2. **Parry (#1457) shipped the exact template dodge needed**: `ParryChance` (base `0`, authored-only) × `ParryChanceMultiplier` (base `1`, the path's bonus target), product computed at the check site.
3. **The #1481 share claims dissolved the Frequency blocker**: the filed "representational signal needs explicit difficulty scaling" concern predates the landed-damage × `φ(investment)` tally, which self-supplies difficulty.

## Decisions

Settled with the project owner during the spike:

1. **The six-attribute portrait — four magnitude anchors plus two amplifier identities, no overlap.**

   | Attribute | Role | Sourcing after this spike |
   | --- | --- | --- |
   | STR / END / INT | magnitude anchors | unchanged |
   | DEX | magnitude anchor (finesse damage) | sheds its `0.001` CDR sliver; deliberately **no** crit hook — a `CriticalChanceMultiplier` derivation would double-dip (damage scaling + crit scaling) and dominate STR for crit-committed physical builds |
   | AGI | **tempo & evasion amplifier** | → `DodgeChanceMultiplier`, → `CooldownBonusMultiplier` |
   | LUK | **proc-payoff amplifier** | → `CriticalChanceMultiplier`, → `ParryChanceMultiplier`; keeps `CriticalDamage` |

   Every AGI/LUK target scales a `0`-based enabler, so both attributes are opt-in by construction — **dormant, not dead**: they light up the moment any matching enabler is fielded (on-brand for the hidden-synergy pillar). Dodge stays AGI-exclusive and parry sits with LUK — two legible identities beat one specialist plus one shallow-everything soup.

2. **Dodge goes opt-in on the parry template** ([#1523](https://github.com/ginderjeremiah/GameServer/issues/1523)). `DodgeChance` loses its AGI derivation (stays base-`0`, authored-only from items/effects); new `DodgeChanceMultiplier` (base `1` + AGI-derived); the dodge check reads the product. Dodge ends up doubly committed — substitutive at the recording site *and* opt-in — which the governing principle permits (it requires at least one), and it closes a real leak: a class locked base that grows AGI no longer opens the Evasion path universally. Enabler texture is deliberate: parry is a timed stance *window*, dodge is passive gear *uptime*.

3. **CDR is severed outright; cadence becomes the fourth enabler × multiplier pair** ([#1524](https://github.com/ginderjeremiah/GameServer/issues/1524)). `CooldownRecovery`'s functional `1.0` base blocks the multiplicative-of-zero trick (#1398 Decision 7), so the AGI/DEX derivations are removed and the committed channel is rebuilt as `CooldownBonus` (base `0`, authored-only) × `CooldownBonusMultiplier` (base `1`, AGI-derived, the Frequency path's bonus target), read at the charge site as `CooldownRecovery + bonus × mult`. Why the pair: flat path rewards would be permanent universal value surviving build abandonment; multiplicative ones would scale the functional `1.0` and lift the uncommitted. `bonus × mult` idles at `0` the moment the enabler is gone. AGI retaining cadence in opt-in form was the owner's call.

4. **Frequency trains as a pseudo-overlay share claim** ([#1527](https://github.com/ginderjeremiah/GameServer/issues/1527)). Faster cycling *is* more hits, so cadence rides every landed player hit: book landed damage × `φ(effective cadence − 1)`, sampled per hit like Cull samples the execute investment. Difficulty is self-supplied (landed basis bounded by the enemy HP pool; power in the accrual denominator) — the filed explicit-difficulty-scaling caveat is obsolete post-#1481.

5. **All six attributes stay allocatable; class fingerprints stay six-wide.** The gear-only alternative was rejected: free-pool investment is the commitment currency the whole design runs on, and stripping AGI/LUK from locked bases invites the same reductio for STR-less Wizards — the locked base is *identity texture*, not an optimization vector. The newbie trap resolves on three facts: the free pool is **freely refundable** (a misallocation costs noticing it, not a character); the allocation screen gains **inert-stat signaling** ([#1528](https://github.com/ginderjeremiah/GameServer/issues/1528)); and the power-measure fix makes dead points *neutral* instead of taxed. (Under today's `SumCoreAttributes` a dead point actively lowers the XP band and every path's accrual — a measurement defect, not an allocation-design defect.)

6. **The power measurement is spun off as its own spike** ([#1526](https://github.com/ginderjeremiah/GameServer/issues/1526)) — owner-flagged as already problematic (player and enemy don't scale equally per core-attribute point). Seed direction: a **combat rating** computed from the assembled battle snapshot — offense rate × survivability, geometric mean, fixed reference profiles — both sides measured through the same function, server-only (no parity surface). Alternatives weighed and disfavored: per-attribute weight tables (meta-chasing), outcome-derived difficulty (sandbagging incentive), level-based power (ignores build). The rating is the keystone that makes Decisions 1 and 5 safe by construction.

7. **Enemy-side attribute meaning, in three tiers.** *(a) Now, authoring discipline:* enemy kits can already scale skill damage/effects off any caster attribute — fingerprints should match kits. *(b) Now, tooling:* a content lint flags enemy distributions nothing in the kit consumes ([#1529](https://github.com/ginderjeremiah/GameServer/issues/1529)) — post-severing, enemy AGI/LUK are always dead weight that inflates XP pricing. *(c) Later, recorded seam:* enemy crit/dodge/parry parity — the opt-in shape makes it safe *content* (an enemy only procs where a designer authors an enabler). Wrinkles recorded for that day: new RNG draws in the shared stream (a real parity surface), offline spike-loss variance, and an enemy riposte needing a signature-skill analog (enemies hold no weapons). Pleasant property: enemy avoidance wouldn't rob player proficiency training — the landed basis still sums to the HP pool (#1481/#1482); dodgy enemies just lengthen fights.

8. **Crit damage's floor stays `1.5` + LUK.** Constraint recorded against #1448's open `1.5×`-vs-`1.0×` knob: at a `1.0` base with no crit-damage source, a crit is a no-op *and Precision is untrainable* (`φ(CriticalDamage − 1) = φ(0) = 0`) — dropping the base therefore requires a co-introduced crit-damage source. The `1.5` floor is the cheapest "welcome to crit" mechanism and is kept ([#1525](https://github.com/ginderjeremiah/GameServer/issues/1525) preserves it alongside the new LUK multiplier derivations).

## Implementation issues

Created as sub-issues of #1426:

| Issue | Area | Scope | Summary |
| --- | --- | --- | --- |
| [#1523](https://github.com/ginderjeremiah/GameServer/issues/1523) — dodge rework | engine / battle (parity) | medium | Decision 2 — `DodgeChance` authored-only × AGI-derived `DodgeChanceMultiplier` |
| [#1524](https://github.com/ginderjeremiah/GameServer/issues/1524) — CDR severing + cadence pair | engine / battle (parity) | medium | Decision 3 — derivations removed; `CooldownBonus × CooldownBonusMultiplier` |
| [#1527](https://github.com/ginderjeremiah/GameServer/issues/1527) — Frequency path | engine / proficiency (parity book) | medium | Decision 4 — cadence share claim; depends on #1524 |
| [#1525](https://github.com/ginderjeremiah/GameServer/issues/1525) — LUK derivations | engine / attributes | small | Decisions 1 & 8 — crit/parry multipliers from Luck; crit damage kept |
| [#1528](https://github.com/ginderjeremiah/GameServer/issues/1528) — inert-stat signaling | frontend | small | Decision 5 — allocation-screen hint for enabler-less amplifiers |
| [#1526](https://github.com/ginderjeremiah/GameServer/issues/1526) — combat-rating power measure | spike / ideation | large | Decision 6 — follow-on spike |
| [#1529](https://github.com/ginderjeremiah/GameServer/issues/1529) — enemy dead-stat lint | content tooling | small | Decision 7b — flags unconsumed enemy distributions |

## Documentation to update on landing

- **[content-design.md](../content-design.md)** — Evasion/Frequency rollout rows firmed up; parking-lot entry resolved; §5 AGI/LUK note updated. _(Done as part of this spike.)_
- **[game-design.md](../game-design.md)** — the dodge portion of *Critical hits, dodge, parry & damage reflection* and the attribute-conventions CDR example rode [#1523](https://github.com/ginderjeremiah/GameServer/issues/1523)/[#1524](https://github.com/ginderjeremiah/GameServer/issues/1524); the Proficiency XP overlay list's cadence pseudo-overlay entry (plus the *Cadence* channel's Frequency-training note) landed with [#1527](https://github.com/ginderjeremiah/GameServer/issues/1527).

## Out of scope / deferred

- **Enemy proc parity** — a recorded seam (Decision 7c), deliberately unscheduled.
- **AGI damage-scaling content** (a "swift" skill family giving AGI a magnitude fallback) — an available content lever, not pursued; the pure amplifier identity is the intent.
- **Exact coefficients** — strawmen in `StaticAttributeModifiers`, tuned during balancing like the rest of the statics.
