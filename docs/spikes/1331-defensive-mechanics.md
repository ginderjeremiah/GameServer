# Spike #1331 — Future defensive mechanics (mitigation→sustain, absorb shield, last-stand)

- **Spike issue:** [#1331](https://github.com/ginderjeremiah/GameServer/issues/1331)
- **Status:** **Research complete; directions documented, awaiting owner input before implementation issues are split out.** No design is committed here — §7 lists the open calls.
- **Related:** deferred out of the mitigation rework ([#1330](./1330-mitigation-rework.md)); builds on the typed-damage substrate ([#1320](./1320-damage-types.md)), the skill-effect system ([#180](./180-skill-effects.md)), and the commitment rule / opt-in-multiplicative template ([#1398](./1398-utility-in-proficiency-system.md), `docs/content-design.md` §4).

## 1. Goal

#1331 parked three defensive ideas when #1330 shipped the Toughness curve and reflection:

- **Mitigation → sustain** — heal for a share of the damage your mitigation absorbed.
- **Absorb shield ("Barrier")** — a temporary damage-absorbing buffer consumed before real HP.
- **Last stand ("Second Wind")** — bonus mitigation/healing below an HP threshold.

The issue's own lean was *"shield/last-stand lean toward the skill-effect system; mitigation→sustain toward an authored attribute."* **That lean is now out of date** — the engine moved underneath it (§2). This spike re-derives each candidate against the substrate as it actually stands today, checks all three against the commitment rule, and surfaces one hard blocker that the original framing did not anticipate (§4).

## 2. What changed since #1331 was filed (2026-06-27)

Three shipped mechanics materially re-rank these candidates:

- **`ExecuteBonus` / Cull ([#1430](https://github.com/ginderjeremiah/GameServer/issues/1430), from spike [#1398](./1398-utility-in-proficiency-system.md)) reads live battle state mid-fire.** The attacker samples the target's missing-health fraction `(MaxHealth − CurrentHealth) / MaxHealth`, clamped `[0,1]`, **once per fire**, and folds it into that fire's damage multiplier — placed in `BattleContext.DamageTarget` ↔ `battle-step`'s `dealPortionedDamage`, i.e. **the fire path, not `ComputeNetDamage`** (§3.1 shows why that placement matters). This is the single most important finding here: **last-stand is the defender-side mirror of a read the engine already performs**, not a net-new mechanism. [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219)'s statement that *"nothing reads live state like a current-health ratio mid-tick"* was true when written and is **no longer accurate** — that issue's cost estimate for conditional passives should be revised down accordingly.

- **Parry/Riposte ([#1457](https://github.com/ginderjeremiah/GameServer/issues/1457)) established the defensive-path tally shape.** A defensive mechanic with no damage output of its own still needs a proficiency training signal; Evasion trains on `BattleStats.DamageDodged` (post-mitigation damage *prevented*, `ProficiencyAccrual:182`) and Riposte on counter damage. So a **prevented-damage tally** is precedented and cheap.

- **`TypeResistanceMitigated` ([#1454](https://github.com/ginderjeremiah/GameServer/issues/1454)) already computes "how much did my resistance block"** (`Battler.cs:186`), deliberately isolated from the Toughness curve. Mitigation→sustain needs exactly this quantity, and its doc comment already argues the case for excluding Toughness (§5.1) — half the design question is pre-answered in code.

## 3. Where each candidate actually sits

Ranked by cost, which **inverts the issue's original framing**:

| Candidate | Original framing | Actual shape today | New state? | Parity surface | Cost |
|---|---|---|---|---|---|
| **Last stand** | "skill effect; needs its own design pass" | Cull's expression on the defender — an authored attribute read per hit | none | one multiplier, but **placement is a real call** (§3.1) | **small** |
| **Mitigation → sustain** | "authored attribute like reflection" | correct, but has a hard blocker (§4) | none | one heal channel after mitigation | **medium** |
| **Absorb shield** | "skill effect, not an attribute" | needs a second health-like pool on `Battler` | **yes — a new state dimension** | wide (§5.3) | **large** |

### 3.1 Last stand — the cheapest, and already half-built

Mirror Cull exactly. An authored-only `LastStandBonus` (base `0`, no `StaticAttributeModifiers` derivation, like `DamageReflection`/`ExecuteBonus`) scales the defender's *own* missing-health fraction into a mitigation multiplier on the incoming hit — **where** that multiplier is applied is itself an open call (see the placement bullet below):

```
missingHpFraction = clamp((MaxHealth − CurrentHealth) / MaxHealth, 0, 1)
net = … × (1 − LastStandBonus × missingHpFraction)
```

- **Continuous, not a threshold step.** The issue says *"below an HP threshold"*, but a step function reintroduces exactly the discontinuous breakpoint pathology #1330 spent a spike removing from flat Defense. Cull already chose the continuous ramp for the offensive mirror; matching it keeps one shape in the codebase and no cliff. **Recommend continuous.**
- **Bounded — but by an authoring invariant, not by construction.** *Provided* `LastStandBonus < 1`, the multiplier stays in `(0, 1]`, so it can reduce a hit but never heal: it cannot invert the sign and cannot reach immunity. That is the property §4 shows sustain lacks, and it is the whole argument for shipping last-stand first — so it must be stated as the assumption it is. **Nothing enforces the bound.** Authored attributes are read unclamped throughout (`DamageReflection` is multiplied raw at `BattleContext.cs:493-499`, and `ComputeNetDamage`'s own comment says *"no clamp is needed"* for the analogous resistance case, `Battler.cs:136-140`), so an authored `LastStandBonus ≥ 1` against a near-dead defender drives the multiplier to `≤ 0` and turns the hit into a heal — exactly the flat-Defense pathology #1330 removed. This is plausibly fine under the house precedent of [#1478](https://github.com/ginderjeremiah/GameServer/issues/1478) (the negative-Toughness pole is left unguarded as *"unreachable by authored content"*), but it is that class of assumption, not a guarantee. Either name the authoring invariant explicitly or specify the clamp. (The interval's open lower bound is likewise conditional: at `LastStandBonus = 1` the multiplier reaches exactly `0`, but only when `missingHpFraction = 1`, which means the defender is already dead.)
- **Where it sits in the pipeline is a real call, and wider than it first looks.** `Battler.ComputeNetDamage` is today a **pure function of attributes**, with four consumers — only one of which is the damage path:
  - `Battler.cs:243` (`TakeDamage`, the real hit) — ✅ wants last-stand.
  - `BattleContext.cs:260` — `Stats.DamageDodged`, which feeds Evasion's proficiency accrual (`ProficiencyAccrual.cs:182`).
  - `BattleContext.cs:245` — the same shape for `DamageParried`.
  - `CombatRating.cs:265` — a **synthetic reference defender**, whose `CurrentHealth` is not a meaningful quantity; the offense rating would silently acquire a term keyed to it.

  So a multiplier added inside `ComputeNetDamage` is not a no-op: it silently changes two accrual signals (arguably *correctly* — those tallies are counterfactual "what would this hit have done", so a health-scaled answer may be right — but that should be a decision, not a side effect) and pollutes the combat rating. **The precedent cuts toward the fire path:** Cull put its live read in `BattleContext.DamageTarget`, which is exactly why it disturbed neither the tallies nor `CombatRating`. "Mirror Cull exactly" therefore argues for the fire path here too. The frontend widens it slightly further — `mitigateDamage` is a free function over attributes (`battle-formulas.ts`), so a health term changes its signature. Open question **Q3**.
- Commitment rule: ✅ authored-only enabler, `0` when uncommitted. Trains on a prevented-damage tally (Evasion's precedent).

### 3.2 Mitigation → sustain — right shape, one hard blocker

Follows reflection's template as the issue said: an authored-only `MitigationSustain` (base `0`), healing a share of what mitigation absorbed, capped at MaxHealth like every other heal channel. The engine substrate is present (`TypeResistanceMitigated`, `CapHealToRoom`, the `ApplyHealOverTime` cap). **But see §4 — as literally specified it has an immortality breakpoint.**

### 3.3 Absorb shield — collides with a load-bearing invariant

*"The game has no overheal/shield concept"* is asserted at **7 code sites — 4 backend (`Battler.cs:237,262,316,416`) and 3 frontend (`battler.ts:220,236,266`), so the invariant is parity-pinned on both simulators** — and **3 doc sites** (`game-design.md:132`, `backend-battle.md:36,73`), and it is the stated *reason* three separate heal channels cap at MaxHealth. A shield is not an increment on that design — it reverses it, and every cap that cites it needs re-deciding. §5.3 enumerates the collision surface.

## 4. The blocker: mitigation→sustain has an immortality breakpoint

This is the finding that most changes the picture, and it is not mentioned in #1331 or #1330.

Let `m = (1 − r)(1 − t)` be the fraction of a hit that survives mitigation (`r` = summed type resistance, `t = Toughness/(Toughness + C)`), and `S = MitigationSustain`. Healing a share of what was absorbed gives:

```
net      = dealt × m
absorbed = dealt × (1 − m)
heal     = absorbed × S
Δhealth  = net − heal = dealt × [ m(1 + S) − S ]
```

So the defender **takes zero or negative net damage** whenever:

```
m ≤ S / (1 + S)
```

That threshold is reachable with ordinary investment, not degenerate stacking:

| Toughness | `t` | `r` | `m` | `S` for immortality |
|---|---|---|---|---|
| 200 (`= C`) | 50% | 0 | 0.50 | 100% |
| 600 (`= 3C`) | 75% | 0 | 0.25 | **33%** |
| 800 (`= 4C`) | 80% | 0.30 | 0.14 | **16%** |

At Toughness 800 with 30% type resistance, a **16% sustain rate makes the battler strictly immune to direct hits** — and the immunity *deepens* as mitigation grows, because more mitigation means more absorbed damage means more healing. Worse, `DamageReflection` keeps working while immortal, so the outcome is not the 2-minute timeout draw a pure wall gets today — it is a **guaranteed win against any non-DoT enemy**.

That last step is worth spelling out, because the obvious objection is that reflection should switch off: `ReflectDamage` early-returns on `netDamage <= 0` (`BattleContext.cs:486-491`). It doesn't apply here. Sustain never drives `net` non-positive — `net` stays exactly what mitigation produced, and the heal is a *separate channel* that simply outruns it. So reflection fires on every hit for the entire battle while the defender's health never falls. The escalation from "unkillable wall" to "wins every fight" is what makes this a blocker rather than a balance note.

**The MaxHealth heal cap does not defuse this.** §3.2 notes the sustain heal would be capped at MaxHealth like every other heal channel, which reads as a safeguard; it isn't one. The cap removes *overheal* only. Past the breakpoint the battler settles into a steady state just below full health — each hit removes `net`, the sustain heal refills the room that hit just made, and `Δhealth` clamps to `0` at the top of the bar instead of going negative. Never dying, forever. The `S ≤ m/(1−m)` analysis is unchanged; the cap only pins the equilibrium rather than letting health run away.

This is precisely the failure class #1330 existed to eliminate: flat Defense's degenerate immunity breakpoint at the `0` floor. Naively coupling healing to mitigation puts a breakpoint back, just further along the curve. DoT is the only residual pressure valve (it bypasses the Toughness curve — `backend-battle.md:36`), and it is content-dependent: against an enemy that authors no DoT, the immunity is absolute.

**Four ways out, with my read on each:**

1. **Scale the heal off `net` instead of `absorbed`** — `Δhealth = dealt × m(1 − S)`, which only reaches zero at `S = 1` and never goes negative. Bounded by construction, but it is no longer *mitigation*→sustain: it is lifesteal-on-damage-taken, which rewards being hit rather than mitigating, inverting the design intent. **Rejects the premise.**
2. **Cap the heal at a fraction of the hit that landed** — `heal = min(absorbed × S, net × k)` with `k < 1`. Guarantees strictly positive net damage on every hit regardless of `S`, `r`, or `t`, while leaving the mechanic mitigation-coupled in the entire normal regime. **My recommendation** — it keeps the identity and hard-bounds the pathology with one clamp.
3. **Route sustain through the HoT accumulator rather than an instant heal** — credits `HealthRegenPerSecond` instead of healing inline, so burst can still kill through it. Softens burst immortality but does **not** fix the steady-state case; the breakpoint math is unchanged over a full battle.
4. **Diminish the coefficient** — run `S` through `φ(a) = a/(1+a)` (`OverlayTally.NormalizeInvestment`) so it saturates below 1. Reduces how *easily* the breakpoint is reached but does not remove it: `φ` saturates toward 1, and §4's table shows `S` values well under 1 already suffice.

Only (2) both preserves the intent and removes the breakpoint rather than relocating it.

## 5. Design questions each candidate raises

### 5.1 Which mitigation counts toward sustain?

`TypeResistanceMitigated`'s doc comment (`Battler.cs:176-189`) already litigated the analogous question for proficiency training and excluded Toughness, reasoning: *"Toughness is a generic, non-typed stat every build can raise, so folding it in would let it accelerate every resist path's training at once."* The same argument applies to sustain, and pulls toward **resistance-only**. Against that: the design intent in #1331 is explicitly *tank*-coupled ("more mitigation → more healing"), and Toughness is the tank's stat — resistance-only sustain would be a *Warding* mechanic, not a tank one. Genuine fork (**Q1**).

### 5.2 Which proficiency path trains it?

`Restoration` (`EActivityKey.Heal`) already exists and trains on `PlayerDamageHealed`. Routing sustain there is free but lets a Toughness-heavy build with one sustain item train Restoration passively off bulk investment — thin, given the commitment rule measures *"excess over a floor that is naturally 0 when uncommitted"*. A dedicated key (an "Aegis"/"Bulwark" Technique path) is cleaner but is net-new content. (**Q4**)

**A note that applies only to the shield:** the commitment rule names **raw EHP** as universal free value that earns **no** path (`content-design.md:178`). An absorb shield *is* raw EHP in temporary form, so under the rule as written it gets no proficiency path — unlike sustain and last-stand, both of which are authored-only enablers that qualify. Either the shield ships path-less, or the rule needs an explicit carve-out. Worth deciding before, not after.

### 5.3 The shield's collision surface

If a shield pool is built, each of these needs an explicit decision — every one is parity-critical:

- Does a shield absorb **DoT** ticks, or direct hits only? (Reflection/dodge/parry are all direct-hit-only; DoT is deliberately the pressure valve.)
- Does damage eaten by the shield count toward the **DamageTaken** statistic?
- Does it feed **`HealthRemoved`** (#1482), the offense-book basis capped at health actually removed? If shield damage books, the "bounded by the enemy's health pool" guarantee that keeps overlay tallies from inflating no longer holds.
- Does shield count in **Cull's missing-health fraction** — is a full-shield, low-HP target executable?
- Is the **reflection** basis `net` before or after the shield eats it?
- Does the **MaxHealth heal cap** apply to the shield pool, and can a heal refill it?

That is six parity-pinned decisions plus a new state field on both simulators, before any content exists. It is the one candidate where the engine work meaningfully exceeds the design work.

## 6. Directions the issue did not list

Per the ideation brief, exploring beyond the three candidates:

- **Part of "absorb shield" is authorable *today*, with zero engine work.** A timed skill effect granting **resistance > 1** on a type already produces reactive absorption (a net heal, `ComputeNetDamage`'s negative branch), and a timed `MaxHealth` buff paired with a HoT approximates a temporary buffer. Neither is a *finite pool* — the feel differs (percentage-based and unbounded in total, vs. a buffer that depletes) — but the "temporary damage sponge" fantasy is partially reachable now. **Recommend authoring a barrier-flavored skill on the existing primitives and playing it before committing to a pool**; it may retire the candidate, and it costs a Workbench entry rather than a spike.
- **Last-stand is half of a channel that spans two spikes.** The parallel ideation spike for [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219) ([`1219-class-system-v2.md`](./1219-class-system-v2.md), in review as PR [#2402](https://github.com/ginderjeremiah/GameServer/pull/2402)) independently reached this doc's §2 conclusion from the offensive side, and pins the relationship precisely: its **rage** passive (damage rises as *your own* health drops) and this spike's **last-stand** (damage taken falls as *your own* health drops) are the offence and defence halves of **one** mechanic. Note the distinction from Cull, which samples the *target's* health — rage and last-stand both sample the **battler's own**, which is a different read at a different site. That argues for designing them as one channel family (a single "own-missing-health fraction" sample, two multipliers hanging off it) rather than two independently-specified attributes that happen to need the same term. Whoever answers Q3 here should answer that spike's equivalent sampling/pricing question at the same time.
- **The three compound.** Sustain + last-stand + high Toughness reach §4's regime far faster than any one alone (last-stand raises `t` exactly when sustain is healing most). If more than one ships, the breakpoint analysis must be run over the *combination*, not per mechanic. This argues for sequencing rather than a bundled release.
- **Anti-burst damage cap** was considered and **not pursued** in #1330 (deliberately avoiding a stat that exists only to counter specific enemies). Nothing found here changes that; it stays rejected.
- **Second Wind as a one-shot revive** (survive a lethal hit at 1 HP, once per battle) is a distinct mechanic from the last-stand ramp and arguably the more evocative reading of the name. It needs per-battle state on `Battler` (a consumed flag) — cheaper than a shield pool but not free — and it is a *hard* discontinuity by nature, so it sits opposite the continuous ramp rather than being a variant of it.

## 7. Open questions for the owner

Not planning these into implementation issues until there is a read on:

- **Q1.** Should sustain credit **resistance-only** mitigation (consistent with `TypeResistanceMitigated`'s reasoning) or **resistance + Toughness** (consistent with #1331's tank-coupled intent)?
- **Q2.** Is §4's option (2) — capping the sustain heal at a fraction of the damage that landed — an acceptable shape, or does the immortality breakpoint make the whole candidate not worth carrying?
- **Q3.** Where does last-stand's bonus go (§3.1)? Three options, not two: **(a) a fire-path multiplier** in `BattleContext.DamageTarget` ↔ `dealPortionedDamage` — Cull's own placement, leaving the two prevented-damage tallies and `CombatRating` untouched; **(b) a multiplier inside `ComputeNetDamage`** — simplest to write, but then `DamageDodged`/`DamageParried` accrual and the synthetic reference defender each need an explicit call; or **(c) additive into `Toughness`**, so it rides the diminishing curve and is self-limiting. (a) is my lean, on the "mirror Cull exactly" argument. Relatedly: is the `LastStandBonus < 1` bound an authoring invariant of the #1478 class, or should it be clamped?
- **Q4.** Do sustain and last-stand earn **new Technique paths**, or route onto **Restoration** / an existing key?
- **Q5.** Is the absorb shield worth reversing the "no overheal/shield concept" invariant at all — or should we try the §6 approximation on existing primitives first and reassess?

**My recommendation if a read is wanted:** ship **last-stand first** (smallest, and bounded under the stated authoring invariant), take **sustain second and only with the §4 cap**, and **defer the shield** behind the §6 content experiment. That ordering also keeps the compounding analysis tractable — one new defensive lever at a time. One coordination note: last-stand should be scoped together with the **rage** passive from the [#1219 spike](./1219-class-system-v2.md) (§6) rather than separately, since they are the two halves of one own-health read.

## 8. Documentation to update on landing

**Re-check this doc's line-numbered citations against `main` in the same pass.** They are unusually load-bearing here — §3.1's four-consumer enumeration is what reframes Q3 from two options into three — and a parked spike doc accumulates rot silently. Three movers landed while this doc sat in review: #2400 (splitting the effect machinery out of `Battler.cs`), #2428 (the `CombatRating` proc-chance clamp), and #2438 (sharing the parry/dodge chance products between the engine and `CombatRating`) — the last of which shifted `Battler.cs` by +18 lines, `BattleContext.cs` by −2 and `battler.ts` by +21 in one go, invalidating **every** code citation in this doc at once. A reader who follows a rotted citation lands on an unrelated line and the argument reads as unsupported. Re-derive the enumeration from `git grep ComputeNetDamage` rather than trusting the line numbers.

**When the implementation issues are split out, also amend [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219)** to strike its *"nothing reads live state like a current-health ratio mid-tick"* claim (§2) and revise its cost estimate for conditional "stance" passives downward. Left in place, that stale claim will be re-cited by the next spike to look at the same ground — which has **already happened**: the [#1219 spike](./1219-class-system-v2.md) (PR [#2402](https://github.com/ginderjeremiah/GameServer/pull/2402)) hit the same line independently, so two spikes now depend on it being corrected.

Whichever land: `docs/game-design.md` (the archetype-split section) and `docs/game-design-combat.md` (a per-mechanic subsection alongside reflection and parry). A shield additionally requires retracting the "no overheal/shield concept" statement from `game-design.md`, `backend-battle.md`, and all seven code comments that cite it.
