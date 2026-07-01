# Spike #1398 — Should utility be worked into the proficiency system?

- **Spike issue:** [#1398](https://github.com/ginderjeremiah/GameServer/issues/1398)
- **Status:** Ideation complete; design decided. Implementation split into sub-issues (see [Implementation issues](#implementation-issues)); one follow-on spike opened ([attribute identity-sourcing](#follow-on-spikes)).
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
3. **Crit vs dodge exposed the real seam.** Dodge is *substitutive* (a dodged hit records no exposure — [`BattleContext.cs`](../../Game.Core/Battle/BattleContext.cs) — so Evasion and resist are mutually exclusive; the opportunity cost is intrinsic). Crit is an *additive overlay* — a crit hit books its full net into both the type path **and** Precision, double-counting. Crit is also the one event key whose magnitude is *not independent* of another book (it re-measures offense damage).
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
- **Tallied magnitude** — the archetype books its own contribution (crit damage, ramp bonus, reflected damage), which is what the path trains on and what makes the commitment *proportional*.

The template's virtue is that it is **one pattern instantiated per archetype**, not a new system each time. Build differentiation scales as **leaves × archetypes**, both axes extensible.

### Candidate archetype roster (design direction, not committed content)

Recorded in [content-design.md](../content-design.md) as planned content; built through the Workbench when the content arc reaches them.

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

## Changes by area / Implementation issues

Created as sub-issues of #1398:

| Issue | Area | Scope | Summary |
| --- | --- | --- | --- |
| [#1425](https://github.com/ginderjeremiah/GameServer/issues/1425) — Crit rework: opt-in multiplicative crit chance | engine / attributes / authoring | medium | Decision 2 — the worked example of the template. |
| [#1426](https://github.com/ginderjeremiah/GameServer/issues/1426) — _Follow-on spike:_ attribute identity-sourcing | ideation / attributes | large | Decision 8 — what `Dexterity`/`Luck`/`Agility` do once identities are opt-in; covers the CDR-severing question. |

The candidate archetypes are tracked as **deferred** enhancement issues — built through the Workbench + engine when the content arc reaches them (not blocking): [#1428](https://github.com/ginderjeremiah/GameServer/issues/1428) Momentum, [#1427](https://github.com/ginderjeremiah/GameServer/issues/1427) Hex, [#1429](https://github.com/ginderjeremiah/GameServer/issues/1429) Sunder, [#1430](https://github.com/ginderjeremiah/GameServer/issues/1430) Cull (the last needs the damage-calc conditional from Decision 4). Everything else the spike produced is **documentation / design intent** (this doc + content-design.md): the template pattern, the attribute-scaling-content resolution, and the amplifier-stays-out rule.

## Documentation to update on landing

- **[content-design.md](../content-design.md)** — resolve the ⚠ utility parking-lot entry; record the delivery-archetype roster + the opt-in-multiplicative template as the differentiation model; record the "author effects as commitments / universal amplifiers are not free value" authoring discipline; note that attribute-concentration synergy is attribute-scaling content; unblock the §5 class-secondary TBDs (a secondary opens a second root via a *committed* effect — a small damage/event portion or an archetype enabler — never a universal buff). _(Done as part of this spike.)_
- **[game-design.md](../game-design.md)** — with the crit rework: rewrite the crit portion of *Critical hits, dodge & damage reflection* for opt-in multiplicative chance (no attribute-derived base; item/skill-sourced; Precision multiplicative; `Luck` retained on crit damage).

## Out of scope / deferred

- **Frequency / CDR proficiency** — reachable via the opt-in route only after CDR's attribute sourcing is severed; folded into the attribute-role spike. Also note frequency is *representational* (a build-state signal, accurate but flat-per-victory) and does not self-supply a difficulty signal, so it would need explicit difficulty scaling — payable, but a real cost.
- **Cull / execute** — the one candidate archetype needing new engine logic (HP-conditional damage). Not committed.
- **Delivery-archetype content** (Momentum / Hex / Sunder) — planned content, authored when the arc reaches them.

## Follow-on spikes

- **Attribute identity-sourcing redesign** ([#1426](https://github.com/ginderjeremiah/GameServer/issues/1426)) — what `Dexterity` / `Luck` / `Agility` become once combat identities (crit, and potentially CDR/dodge) move from attribute derivations to opt-in enablers: multiplicative-amplifier vs direct-removal per stat, whether they remain allocatable attributes or become gear-only, and the newbie-trap of allocating into a now-inert stat. Seeded by Decision 8.
