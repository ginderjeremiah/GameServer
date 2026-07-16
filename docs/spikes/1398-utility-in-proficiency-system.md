# Spike #1398 — Should utility be worked into the proficiency system?

- **Spike issue:** [#1398](https://github.com/ginderjeremiah/GameServer/issues/1398)
- **Status:** Ideation complete; design decided. Implementation split into sub-issues (see [Implementation issues](#changes-by-area--implementation-issues)); one follow-on spike opened ([attribute identity-sourcing](#follow-on-spikes)).
- **Related:** reworks the effect-based accrual from spike [#1318](https://github.com/ginderjeremiah/GameServer/issues/1318); touches the crit sourcing from [#178](./178-player-crit-dodge-block.md); unblocks the class-secondary TBDs and the archetype planning in [content-design.md](../content-design.md) (§4–§8).

## The question, as filed

> A skill that exists only to buff CDR does not contribute towards any proficiency.
> - Should it contribute to a proficiency path?
> - What benefits would said proficiency path provide?
> - How is the contribution codified?

The literal example (a CDR buff) turned out to be the *worst* representative of the real problem, and "utility" turned out to be the wrong unit of analysis. The useful output of this spike is a **principle for what earns a proficiency track** and a **template for authoring build identities**, from which the utility question falls out as a corollary.

## The reframe — how the answer evolved

The discussion walked through several framings before landing. Recording the path so it isn't re-tread:

1. **"Utility's value is already captured."** In a solo game every buff is a self-buff, so a STR/CDR buff amplifies the combat magnitudes that already accrue (and, because `power` is frozen at battle start, a mid-battle self-buff even raises `activity ÷ power`). True for *amplifier* utility — but too blunt as a blanket answer.
2. **Build identity is an *axis*, not a buff.** The interesting question is fast-weak vs slow-heavy — a **tradeoff dial**, orthogonal to damage type. Proficiency (an accumulator model) natively fits accumulator axes, not dials.
3. **Crit vs dodge exposed the real seam.** Dodge is *substitutive* (a dodged hit records no exposure — [`BattleContext.cs`](../../Game.Core/Battle/BattleContext.cs) — so Evasion and resist are mutually exclusive; the opportunity cost is intrinsic). Crit is an *additive overlay* — a crit hit books its full net into both the type path **and** Precision, double-counting. Crit is also the one event key whose magnitude is *not independent* of another book (it re-measures offense damage). _(Later refined: that "double-counting" is **harmless** under the non-zero-sum accrual — the real flaw is the **full-hit** magnitude, revised to a normalized marginal in the [Follow-on refinement](#follow-on-refinement--the-overlay-tally-shape-normalized-marginal).)_
4. **Opt-in multiplicative crit (owner's idea).** Because crit chance has **no base** (`0` until fed), removing its attribute derivation and making Precision *multiplicative* makes crit **fully opt-in**: `0 × any multiplier = 0`, so a build without a crit enabler never crits and Precision does nothing. This kills crit's universality *legibly* (multiplicative-of-a-visible-base is a standard pattern) and makes crit a committed axis whose parallel Precision XP is plainly earned.
5. **The delivery-archetype roster.** Build differentiation comes from the *roster of delivery styles* (crit / DoT / reflection today), not the leaves. Crit's opt-in template generalizes: **flat enabler in gear/skills + multiplicative path + tallied magnitude**. Momentum / Hex / Sunder are the same shape; Cull (execute) is the exception that needs new engine logic.
6. **Universal vs committed is the gate.** The discriminator was never "is it a buff." A buff/debuff is just the *mechanism*. What earns a path is **commitment** — access through an opt-in enabler with a real opportunity cost — so the parallel XP is earned. *Universal* effects (benefit every build for free) are disqualified.
7. **Commitment = expectation-0.** "Measure excess over an expectation" is the general form. Crit's expectation is `0` (no base). A stat with a nonzero **functional base** (CDR's `1.0`) would need an authored `expectation(power)` curve — a *maintained* balance liability that chases the meta — so the clean move is to **sever its incidental attribute sourcing** and restore the `0` floor, exactly as crit does.
8. **Attribute-concentration is a different axis.** "All-in STR → STR synergies" is *emergent from allocation*, inherently expectation-relative, and **not** captured by the type/umbrella taxonomy (a DEX physical build conflates with STR; magic is designed to *spread* for synthesis, so concentration-reward fights it, and the umbrella is a late capstone anyway). It is served by **attribute-scaling content** (skills/items that scale super-linearly with an attribute), which is orthogonal to type and opt-in — not by a proficiency track.

## The governing principle

> **A proficiency path rewards a *committed* build identity. Author combat identities as commitments — never as universal free value — and measure them as *excess over a floor that is naturally `0` when uncommitted* (expectation-0, via an opt-in enabler). The universal-vs-committed test is the enforcement check, not a filter with an awkward exception.**

Two ways an identity carries legible commitment (an identity needs at least one):

- **Substitutive accrual** — mutually exclusive with a sibling at the recording site (dodge ⟂ resist; type ⟂ type via focus-beats-spread). Opportunity cost on the *earning* side. Legible only when the exclusion is physically obvious.
- **Opt-in enabler + build-specific rewards** — the identity is `0` until authored in (a crit dagger, a ramp skill), and the path's rewards idle for anyone who abandons the build. Opportunity cost on the *access* and *payoff* sides. Always legible.

Corollaries that resolve the filed question:

- **Universal amplifiers stay out.** A raw STR/CDR buff authored as free value benefits every build; path-ifying it recreates the original crit problem. Its payoff is the paths it amplifies (and, for cadence, farm throughput). **The filed CDR-buff example lives here → no path.** Most stat/type buffs are *already* committed by scaling/type specificity (a +STR buff does ~nothing for an INT mage); the genuinely universal residue is small (flat CDR, all-damage%, raw EHP) and should be authored as conditional or tradeoff effects, not free value.
- **Committed effects come in via the template.** The buff/debuff *mechanism* is exactly how delivery archetypes are delivered; commitment (not utility-ness) is the gate.
- **Proficiency is a single-currency (attribute-magnitude) measure.** The `activity ÷ power` accrual works because damage *is* attribute-derived and enemy-set magnitude self-supplies the difficulty signal. Axes not in that currency (frequency) are reachable but cost their own baseline + explicit difficulty scaling — see [deferred](#out-of-scope--deferred).

## The opt-in-multiplicative template

The pattern every delivery archetype follows (crit is instance #1):

- **Flat enabler** authored into **items / skills**, with **no attribute-derived base** — so the enabling stat is `0` until the player opts in.
- **Multiplicative path** — the Technique proficiency scales the committed base (`0 × mult = 0`, so it is inert for the uncommitted).
- **Tallied magnitude** — the archetype books its own contribution (crit damage, ramp bonus, reflected damage), which is what the path trains on and what makes the commitment *proportional*. **How** that contribution is measured — a normalized *marginal*, not the full hit — is settled in the [Follow-on refinement](#follow-on-refinement--the-overlay-tally-shape-normalized-marginal) below.

The template's virtue is that it is **one pattern instantiated per archetype**, not a new system each time. Build differentiation scales as **leaves × archetypes**, both axes extensible.

## Follow-on refinement — the overlay tally shape (normalized marginal)

_Added after the spike closed, resolving **how** the "tallied magnitude" is actually measured — the shape the template left open. The discussion started from the Hex sub-issue ([#1427](https://github.com/ginderjeremiah/GameServer/issues/1427)) and generalizes to every **overlay** archetype — one that rides on a baseline hit that *would have landed anyway*: **crit, Hex, Sunder, Momentum, Cull**. Reflection and DoT are **not** overlays — reflected damage is 100% enabler-caused (its full tally already *is* its marginal) and DoT is primary typed damage — so they are unaffected._

**The double-count is harmless; the real question is strength-proportionality.** Accrual is **non-zero-sum** — each path independently claims `pie × clamp(activity ÷ power)` ([`ProficiencyXpCalculator`](../../Game.Core/Proficiencies/ProficiencyXpCalculator.cs)), so a crit hit training *both* its type path and Precision robs neither. "Double-counting" is not the defect. The defect a **full-hit** tally has is that its magnitude is **not proportional to the commitment's strength**: it folds in the base damage that would have landed anyway, so investing in a *stronger* overlay (more crit damage, a deeper vulnerability) barely moves the tally. Crit ships full-hit today and it is *tolerable* only because a crit's bonus is a sizeable fraction of its hit — the same full-hit tally on Hex (whose marginal is a sliver on top of the full landing) would train a token debuff as fast as a committed one. So the decision is **marginal, applied uniformly** (crit included).

**Four resolutions (the shape):** _(resolutions 2–3 were later superseded — see [Second refinement](#second-refinement--share-claims-supersede-the-counterfactual-marginal-1481) below)_

1. **Marginal, normalized — uniform across overlays.** Tally the archetype's **marginal contribution** (the extra damage it enabled), not the full hit, passed through a **concave, saturating normalization** `φ` so the training rate rewards magnitude investment without a raw-linear runaway (weak still trains proportionally; a huge magnitude saturates rather than blowing through the curve). Whatever `φ` is chosen is applied to **crit as well** — consistency across the template.
2. **Fixed-baseline _add-one-in_, never leave-one-out.** With several overlays stacking multiplicatively (`net = base × amp × crit × (1−resist) × (1−tough)`), each overlay's marginal must be measured against a **fixed baseline** — the vanilla net with *every* player overlay neutralized — as `N(only overlay i) − N₀`, **not** leave-one-out from the fully-amplified state (`N(all) − N(all except i)`). Leave-one-out double-counts the multiplicative synergy into every overlay (the tallies sum to more than the total extra); add-one-in credits each its standalone contribution, leaves the synergy unattributed, and — critically — makes each overlay's tally **independent of what else is stacked** (your Hex training does not balloon because you also crit).
3. **Absolute marginal, not the ratio — normalize on the player's investment.** Tally the **absolute damage enabled** (`D × v` for Hex, `D × (m−1) × mitigation` for crit), not the damage *multiplier*. A resistance debuff's multiplier is **hyperbolic in the enemy's resistance** (−50 resist points is `1.5×` vs a soft enemy but `6×` vs a 90%-resist one), but the *absolute* enabled damage is **flat** (`D × v` — the base resist cancels), so an absolute tally keeps a mitigation-side archetype **enemy-independent**: it does not train faster against resistant enemies. Consequently `φ` is applied to the **investment magnitude** (`v` for Hex, `m−1` for crit), never to the enemy-dependent amplification ratio `marginal ÷ baseline`.
4. **Crit is the worked reference implementation.** Exactly as [#1425](https://github.com/ginderjeremiah/GameServer/issues/1425) was the worked example of the opt-in-multiplicative shape, migrating crit from full-hit to normalized-marginal (parity-critical: FE/BE mirrored + matching tests) is the worked example of the **tally** shape. Every other overlay then copies a proven pattern instead of re-deriving attribution. Tracked as [#1448](https://github.com/ginderjeremiah/GameServer/issues/1448).

**Still open (knobs, not blockers):**

- The exact `φ` curve — starting candidate `a / (1 + a)`, which bounds an overlay's contribution at one baseline hit; worth a short playtest against the proficiency curve.
- Crit's base multiplier `1.5×` vs `1.0×`: `1.5×` credits the enabler's free floor (consistent with crediting a Hex enabler's base vulnerability); `1.0×` makes crit purely investment-gated (crit chance and crit-damage co-required). Sets where `φ`'s low end starts.
- Proc-based vs deterministic Hex/Sunder enablers — deferred; does not affect the tally rule.

**Gameplay-tuning note (distinct from the tally).** Because a mitigation debuff's *value* is hyperbolic in the enemy's mitigation, a strong enough vulnerability can trivialize a high-resist wall. That is a **content-tuning** concern (authored resist values × debuff strengths), separate from XP attribution — flag it when authoring Hex/Sunder enablers so a debuff cannot erase a resist-gated boss.

### Second refinement — share claims supersede the counterfactual marginal (#1481)

_Added 2026-07, after Sunder ([#1429](https://github.com/ginderjeremiah/GameServer/issues/1429)) shipped the first no-counterfactual tally. The remaining literal-marginal tallies turned out to violate resolution 2 in practice: their baselines run at the target's **live** (debuffed) mitigation, so the debuff archetypes inflate crit/Momentum/Cull first-order (a 0.4 vulnerability at 30% enemy resistance inflates the crit baseline ≈1.57×), and a Sunder debuff inflates Hex through the live Toughness factor. This was latent when the crit reference (#1448) landed — nothing moved mitigation mid-battle back then._

**Resolution (supersedes resolutions 2–3):** every overlay books the same **share claim** — the hit's **landed** damage (post-mitigation net, capped at the health actually removed, [#1482](https://github.com/ginderjeremiah/GameServer/issues/1482)) × `φ(its own investment)`. No fixed baselines, no counterfactual curve evaluations. The properties the old machinery bought are carried structurally:

- **Strength-proportionality** — the requirement this whole section opened with — lives in `φ(investment)`; the marginal was only ever one way to compute an attribution, as Sunder's proxy already established.
- **Anti-inflation**: the landed basis sums to at most the enemy's health pool per won battle, so per-battle claims are enemy-authored — stacking overlays shortens the fight instead of ballooning the other tallies, which is what add-one-in was protecting against.
- **Enemy-independence** (resolution 3's goal) holds at the **accrual level** (per-battle claim ≈ coverage share of the HP pool × `φ`) rather than per-hit, and the hyperbolic resist-farming exploit stays impossible (the basis is mitigated damage, never a ratio).
- The accrual's own non-zero-sum, overlapping-claims model (`ProficiencyXpCalculator`) is the native philosophy here — each path claims a `φ`-scaled share of the real output.

Resolutions 1 and 4 stand: the tally is uniform across all five overlays (crit included), and crit remains the reference implementation. Implemented in [#1481](https://github.com/ginderjeremiah/GameServer/issues/1481), on top of the overkill booking cap ([#1482](https://github.com/ginderjeremiah/GameServer/issues/1482)).

### Candidate archetype roster (committed direction; content placement follows the arc)

Recorded in [content-design.md](../content-design.md). These are **committed, implementable** archetypes — the engine work (path + enabler + tally) is ready to pick up and does **not** wait on a zone. Only each archetype's **content placement** (which zone/challenge introduces its enabler) follows the arc, and that is a separate authoring step, never a prerequisite for building the mechanic.

| Archetype | Style | Tallied magnitude | Engine cost |
| --- | --- | --- | --- |
| **Crit** (Precision) | burst / variance | crit bonus | *reworked here* (opt-in multiplicative chance) |
| **DoT** (Affliction) | attrition | typed DoT dealt | shipped |
| **Reflection** (Retribution) | defensive conversion | reflected damage | shipped |
| **Momentum** ([#1428](https://github.com/ginderjeremiah/GameServer/issues/1428)) | in-battle escalation | ramp bonus | expressible today via stacking self-buffs |
| **Hex** ([#1427](https://github.com/ginderjeremiah/GameServer/issues/1427)) | vulnerability / debuffer | damage enabled by applied vulnerability (marginal) | expressible today (negative resist) |
| **Sunder** ([#1429](https://github.com/ginderjeremiah/GameServer/issues/1429)) | anti-mitigation | mitigation bypassed (marginal) | expressible today (toughness/resist debuff) |
| **Cull** ([#1430](https://github.com/ginderjeremiah/GameServer/issues/1430)) | execute / finisher | bonus vs low HP | **needs new damage-calc conditional** |

## Decisions

Settled with the project owner during the spike:

1. **Adopt the universal-vs-committed / expectation-0 principle** as the governing rule for what earns a proficiency track (above). Author combat identities as commitments.
2. **Rework crit to opt-in multiplicative chance.** Drop the `Dexterity`/`Luck` → `CriticalChance` derivations ([`StaticAttributeModifiers.cs`](../../Game.Core/Attributes/Modifiers/StaticAttributeModifiers.cs)); source flat crit chance from **items/skills only** (chance has no base, so an un-enabled build sits at `0`); make the Precision path's crit-chance bonus **multiplicative**. **Keep** the `Luck` → `CriticalDamage` derivation (it is inert without chance, so it stays opt-in-gated and gives a "welcome to crit" payoff the moment you enable). This reverses [#178](./178-player-crit-dodge-block.md)'s deliberate "crit live from raw allocations" — that convenience *was* the universality.
3. **Document the opt-in-multiplicative template** as the authoring pattern for delivery archetypes, with the candidate roster as design direction in content-design.md.
4. **Cull needs engine work** (an HP-conditional in the damage calc); it is a candidate, not committed — flagged so it is not mistaken for content-only.
5. **Attribute-concentration synergy is content, not proficiency.** Delivered by opt-in **attribute-scaling** skills/items (orthogonal to type, so it distinguishes STR from DEX-physical and does not fight magic-school spread). The proficiency taxonomy stays type/delivery-based.
6. **Universal amplifiers stay out** of proficiency, including the filed CDR-buff example. The residual truly-universal stats (flat CDR, all-damage%, raw EHP) must be authored as conditional/tradeoff effects.
7. **Frequency/CDR proficiency is deferred, not rejected.** Reachable via the same opt-in route *iff* CDR's incidental attribute sourcing is severed (its `1.0` functional base blocks the multiplicative-of-zero trick, so multiplicative conversion does not de-universalize it — direct removal does). Entangled with the attribute-role question → routed to the follow-on spike.
8. **The attribute identity-sourcing ripple is its own spike.** Making crit (and potentially CDR/dodge) opt-in empties `Dexterity`/`Luck`/`Agility` of the derived stats they currently source, turning them into specialist identity-amplifiers and raising a newbie-trap/allocation question. Owner's seed direction: crit's attributes → **multiplicative** (works because chance is `0`-based; `Luck` can also stay on crit *damage*); CDR → **direct removal** (functional base blocks the multiplicative fix). Dodge is the middle case (chance is `0`-based like crit, but substitutive and, once CDR is stripped from `Agility`, narrow enough to be committed already).
9. **_(Follow-on)_ Overlay archetype tallies are normalized _marginal_, not full-hit — uniform across crit and the overlays.** The full detail is in [Follow-on refinement](#follow-on-refinement--the-overlay-tally-shape-normalized-marginal): marginal (not full-hit), normalized by a concave `φ`; measured **add-one-in** against a fixed baseline (not leave-one-out); the **absolute** damage enabled (not the ratio), with `φ` on the player's investment so mitigation-side archetypes stay enemy-independent. Crit ([#1448](https://github.com/ginderjeremiah/GameServer/issues/1448)) is the worked reference; this revises the full-hit tally that shipped under Decision 2.

## Changes by area / Implementation issues

Created as sub-issues of #1398:

| Issue | Area | Scope | Summary |
| --- | --- | --- | --- |
| [#1425](https://github.com/ginderjeremiah/GameServer/issues/1425) — Crit rework: opt-in multiplicative crit chance | engine / attributes / authoring | medium | Decision 2 — the worked example of the template. |
| [#1426](https://github.com/ginderjeremiah/GameServer/issues/1426) — _Follow-on spike:_ attribute identity-sourcing | ideation / attributes | large | Decision 8 — what `Dexterity`/`Luck`/`Agility` do once identities are opt-in; covers the CDR-severing question. |
| [#1448](https://github.com/ginderjeremiah/GameServer/issues/1448) — Crit tally: full-hit → normalized-marginal | engine / battle (parity) | large | Decision 9 — the worked reference for the overlay tally shape; the archetype issues copy it. |

The archetype issues are **ready-to-implement engine features** — the mechanic (path + enabler + normalized-marginal tally) is buildable now and their only real dependency is the crit reference ([#1448](https://github.com/ginderjeremiah/GameServer/issues/1448)) establishing the tally pattern; **content placement** (which zone introduces each enabler) follows the arc but does **not** gate the engine work: [#1428](https://github.com/ginderjeremiah/GameServer/issues/1428) Momentum, [#1427](https://github.com/ginderjeremiah/GameServer/issues/1427) Hex, [#1429](https://github.com/ginderjeremiah/GameServer/issues/1429) Sunder, [#1430](https://github.com/ginderjeremiah/GameServer/issues/1430) Cull (the last additionally needs the damage-calc conditional from Decision 4). Everything else the spike produced is **documentation / design intent** (this doc + content-design.md): the template pattern, the attribute-scaling-content resolution, and the amplifier-stays-out rule.

## Documentation to update on landing

- **[content-design.md](../content-design.md)** — resolve the ⚠ utility parking-lot entry; record the delivery-archetype roster + the opt-in-multiplicative template as the differentiation model; record the "author effects as commitments / universal amplifiers are not free value" authoring discipline; note that attribute-concentration synergy is attribute-scaling content; unblock the §5 class-secondary TBDs (a secondary opens a second root via a *committed* effect — a small damage/event portion or an archetype enabler — never a universal buff). _(Done as part of this spike.)_
- **[game-design.md](../game-design.md)** — with the crit rework: rewrite the crit portion of *Critical hits, dodge & damage reflection* for opt-in multiplicative chance (no attribute-derived base; item/skill-sourced; Precision multiplicative; `Luck` retained on crit damage). _(Done.)_
- **[game-design.md](../game-design.md) + [backend-battle.md](../backend-battle.md)** — with the **crit-tally migration** ([#1448](https://github.com/ginderjeremiah/GameServer/issues/1448), Decision 9): update *Critical hits, dodge & damage reflection* and *Proficiency XP* so Precision trains on the normalized crit **bonus** (marginal), not the full hit, and the seeded-crit tally description in backend-battle.md. _(Done — landed with #1448.)_

## Out of scope / deferred

- **Frequency / CDR proficiency** — reachable via the opt-in route only after CDR's attribute sourcing is severed; folded into the attribute-role spike. Also note frequency is *representational* (a build-state signal, accurate but flat-per-victory) and does not self-supply a difficulty signal, so it would need explicit difficulty scaling — payable, but a real cost.
- **Archetype _content authoring_** (which zone/challenge introduces each enabler, the enabler's authored values) — this is the arc-driven part, authored through the Workbench when the arc reaches it. The **engine mechanics** for Momentum / Hex / Sunder / Cull are **not** out of scope — they are ready-to-implement (see [Implementation issues](#changes-by-area--implementation-issues)); only their content placement waits on the arc.
- **Cull's damage-calc conditional** is the one archetype carrying genuinely new engine logic (an HP-conditional in the damage calc), on top of the shared tally shape.

## Follow-on spikes

- **Attribute identity-sourcing redesign** ([#1426](https://github.com/ginderjeremiah/GameServer/issues/1426)) — what `Dexterity` / `Luck` / `Agility` become once combat identities (crit, and potentially CDR/dodge) move from attribute derivations to opt-in enablers: multiplicative-amplifier vs direct-removal per stat, whether they remain allocatable attributes or become gear-only, and the newbie-trap of allocating into a now-inert stat. Seeded by Decision 8. **Resolved** — see [1426-attribute-identity-sourcing.md](./1426-attribute-identity-sourcing.md).
