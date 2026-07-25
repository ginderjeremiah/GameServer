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

- **`ExecuteBonus` / Cull ([#1430](./1398-utility-in-proficiency-system.md)) reads live battle state mid-fire.** The attacker samples the target's missing-health fraction `(MaxHealth − CurrentHealth) / MaxHealth`, clamped `[0,1]`, **once per fire**, and folds it into that fire's damage multiplier (`BattleContext.DamageTarget` ↔ `battle-step`'s `dealPortionedDamage`). This is the single most important finding here: **last-stand is the defender-side mirror of a read the engine already performs**, not a net-new mechanism. [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219)'s statement that *"nothing reads live state like a current-health ratio mid-tick"* was true when written and is **no longer accurate** — that issue's cost estimate for conditional passives should be revised down accordingly.

- **Parry/Riposte ([#1457](https://github.com/ginderjeremiah/GameServer/issues/1457)) established the defensive-path tally shape.** A defensive mechanic with no damage output of its own still needs a proficiency training signal; Evasion trains on `BattleStats.DamageDodged` (post-mitigation damage *prevented*, `ProficiencyAccrual:182`) and Riposte on counter damage. So a **prevented-damage tally** is precedented and cheap.

- **`TypeResistanceMitigated` ([#1454](https://github.com/ginderjeremiah/GameServer/issues/1454)) already computes "how much did my resistance block"** (`Battler.cs:168`), deliberately isolated from the Toughness curve. Mitigation→sustain needs exactly this quantity, and its doc comment already argues the case for excluding Toughness (§5.1) — half the design question is pre-answered in code.

## 3. Where each candidate actually sits

Ranked by cost, which **inverts the issue's original framing**:

| Candidate | Original framing | Actual shape today | New state? | Parity surface | Cost |
|---|---|---|---|---|---|
| **Last stand** | "skill effect; needs its own design pass" | Cull's expression on the defender — an authored attribute read per hit | none | one multiplier in `ComputeNetDamage` | **small** |
| **Mitigation → sustain** | "authored attribute like reflection" | correct, but has a hard blocker (§4) | none | one heal channel after mitigation | **medium** |
| **Absorb shield** | "skill effect, not an attribute" | needs a second health-like pool on `Battler` | **yes — a new state dimension** | wide (§5.3) | **large** |

### 3.1 Last stand — the cheapest, and already half-built

Mirror Cull exactly. An authored-only `LastStandBonus` (base `0`, no `StaticAttributeModifiers` derivation, like `DamageReflection`/`ExecuteBonus`) scales the defender's *own* missing-health fraction into a mitigation multiplier applied inside `ComputeNetDamage`:

```
missingHpFraction = clamp((MaxHealth − CurrentHealth) / MaxHealth, 0, 1)
net = … × (1 − LastStandBonus × missingHpFraction)
```

- **Continuous, not a threshold step.** The issue says *"below an HP threshold"*, but a step function reintroduces exactly the discontinuous breakpoint pathology #1330 spent a spike removing from flat Defense. Cull already chose the continuous ramp for the offensive mirror; matching it keeps one shape in the codebase and no cliff. **Recommend continuous.**
- **Bounded by construction.** With `LastStandBonus < 1` the multiplier stays in `(0, 1]` — it can reduce a hit but never heal, so it cannot invert the sign and cannot reach immunity. This is the property §4 shows sustain lacks.
- **Where it sits in the pipeline is a real call** — folding it in alongside the Toughness curve (so it multiplies) vs. adding to `Toughness` itself (so it rides the diminishing curve) give very different endgame behavior. Multiplying is simpler and legible; adding is self-limiting. Open question **Q3**.
- Commitment rule: ✅ authored-only enabler, `0` when uncommitted. Trains on a prevented-damage tally (Evasion's precedent).

### 3.2 Mitigation → sustain — right shape, one hard blocker

Follows reflection's template as the issue said: an authored-only `MitigationSustain` (base `0`), healing a share of what mitigation absorbed, capped at MaxHealth like every other heal channel. The engine substrate is present (`TypeResistanceMitigated`, `CapHealToRoom`, the `ApplyHealOverTime` cap). **But see §4 — as literally specified it has an immortality breakpoint.**

### 3.3 Absorb shield — collides with a load-bearing invariant

*"The game has no overheal/shield concept"* is asserted in **4 code sites** (`Battler.cs:219,244,298,398`, mirrored in `battler.ts:199,215,245`) and **3 doc sites** (`game-design.md:132`, `backend-battle.md:36,73`), and it is the stated *reason* three separate heal channels cap at MaxHealth. A shield is not an increment on that design — it reverses it, and every cap that cites it needs re-deciding. §5.3 enumerates the collision surface.

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

This is precisely the failure class #1330 existed to eliminate: flat Defense's degenerate immunity breakpoint at the `0` floor. Naively coupling healing to mitigation puts a breakpoint back, just further along the curve. DoT is the only residual pressure valve (it bypasses the Toughness curve — `backend-battle.md:34`), and it is content-dependent: against an enemy that authors no DoT, the immunity is absolute.

**Four ways out, with my read on each:**

1. **Scale the heal off `net` instead of `absorbed`** — `Δhealth = dealt × m(1 − S)`, which only reaches zero at `S = 1` and never goes negative. Bounded by construction, but it is no longer *mitigation*→sustain: it is lifesteal-on-damage-taken, which rewards being hit rather than mitigating, inverting the design intent. **Rejects the premise.**
2. **Cap the heal at a fraction of the hit that landed** — `heal = min(absorbed × S, net × k)` with `k < 1`. Guarantees strictly positive net damage on every hit regardless of `S`, `r`, or `t`, while leaving the mechanic mitigation-coupled in the entire normal regime. **My recommendation** — it keeps the identity and hard-bounds the pathology with one clamp.
3. **Route sustain through the HoT accumulator rather than an instant heal** — credits `HealthRegenPerSecond` instead of healing inline, so burst can still kill through it. Softens burst immortality but does **not** fix the steady-state case; the breakpoint math is unchanged over a full battle.
4. **Diminish the coefficient** — run `S` through `φ(a) = a/(1+a)` (`OverlayTally.NormalizeInvestment`) so it saturates below 1. Reduces how *easily* the breakpoint is reached but does not remove it: `φ` saturates toward 1, and §4's table shows `S` values well under 1 already suffice.

Only (2) both preserves the intent and removes the breakpoint rather than relocating it.

## 5. Design questions each candidate raises

### 5.1 Which mitigation counts toward sustain?

`TypeResistanceMitigated`'s doc comment (`Battler.cs:158-171`) already litigated the analogous question for proficiency training and excluded Toughness, reasoning: *"Toughness is a generic, non-typed stat every build can raise, so folding it in would let it accelerate every resist path's training at once."* The same argument applies to sustain, and pulls toward **resistance-only**. Against that: the design intent in #1331 is explicitly *tank*-coupled ("more mitigation → more healing"), and Toughness is the tank's stat — resistance-only sustain would be a *Warding* mechanic, not a tank one. Genuine fork (**Q1**).

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
- **The three compound.** Sustain + last-stand + high Toughness reach §4's regime far faster than any one alone (last-stand raises `t` exactly when sustain is healing most). If more than one ships, the breakpoint analysis must be run over the *combination*, not per mechanic. This argues for sequencing rather than a bundled release.
- **Anti-burst damage cap** was considered and **not pursued** in #1330 (deliberately avoiding a stat that exists only to counter specific enemies). Nothing found here changes that; it stays rejected.
- **Second Wind as a one-shot revive** (survive a lethal hit at 1 HP, once per battle) is a distinct mechanic from the last-stand ramp and arguably the more evocative reading of the name. It needs per-battle state on `Battler` (a consumed flag) — cheaper than a shield pool but not free — and it is a *hard* discontinuity by nature, so it sits opposite the continuous ramp rather than being a variant of it.

## 7. Open questions for the owner

Not planning these into implementation issues until there is a read on:

- **Q1.** Should sustain credit **resistance-only** mitigation (consistent with `TypeResistanceMitigated`'s reasoning) or **resistance + Toughness** (consistent with #1331's tank-coupled intent)?
- **Q2.** Is §4's option (2) — capping the sustain heal at a fraction of the damage that landed — an acceptable shape, or does the immortality breakpoint make the whole candidate not worth carrying?
- **Q3.** For last-stand: fold the bonus as a **separate multiplier** in `ComputeNetDamage` (simple, legible) or **add into `Toughness`** so it rides the diminishing curve (self-limiting)?
- **Q4.** Do sustain and last-stand earn **new Technique paths**, or route onto **Restoration** / an existing key?
- **Q5.** Is the absorb shield worth reversing the "no overheal/shield concept" invariant at all — or should we try the §6 approximation on existing primitives first and reassess?

**My recommendation if a read is wanted:** ship **last-stand first** (smallest, bounded by construction, mirrors a shipped mechanic), take **sustain second and only with the §4 cap**, and **defer the shield** behind the §6 content experiment. That ordering also keeps the compounding analysis tractable — one new defensive lever at a time.

## 8. Documentation to update on landing

Whichever land: `docs/game-design.md` (the archetype-split section) and `docs/game-design-combat.md` (a per-mechanic subsection alongside reflection and parry). A shield additionally requires retracting the "no overheal/shield concept" statement from `game-design.md`, `backend-battle.md`, and all seven code comments that cite it.
