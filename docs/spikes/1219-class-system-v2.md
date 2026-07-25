# Spike #1219 — Class system V2: in-class progression bonus + conditional "stance" passives

- **Spike issue:** [#1219](https://github.com/ginderjeremiah/GameServer/issues/1219)
- **Status:** Research complete; **no design committed and no implementation issues created yet** — the open
  questions in [§7](#7-open-questions) are the ask, per the ideation rule (collaborator input before planning).
- **Parent:** the class-system spike [#1126](./1126-class-system.md), which deferred both directions here.
- **Related:** proficiency accrual ([#1318](https://github.com/ginderjeremiah/GameServer/issues/1318),
  [#1526](./1526-combat-rating-power-measure.md)); the delivery-archetype commitment rule
  ([#1398](./1398-utility-in-proficiency-system.md), `content-design.md` §4); Cull
  ([#1430](https://github.com/ginderjeremiah/GameServer/issues/1430)) and Parry/Riposte
  ([#1457](https://github.com/ginderjeremiah/GameServer/issues/1457)), both shipped since #1219 was written;
  the deferred defensive mechanics ([#1331](https://github.com/ginderjeremiah/GameServer/issues/1331)) — see
  [§5](#5-overlap-with-1331).

## Headline

**The engine moved underneath #1219, and it re-ranks both directions in opposite directions.**

- **Direction 2 (conditional "stance" passives) is the cheaper of the two and is mostly not a class feature at
  all.** Its stated cost — *"net-new parity-sensitive battle-sim logic — nothing reads live state like a
  current-health ratio mid-tick"* — is no longer accurate. All three of its examples decompose into engine
  channels that already exist, are one Cull-shaped multiplier away, or are already tracked as a skill-effect
  extension elsewhere. The class seam (`ClassSignaturePassive` → one `AttributeModifier`) already delivers
  whatever channel exists, so "stance passives" is a **combat-channel + content** question, not class machinery.
- **Direction 1 (in-class progression bonus) is cleaner than #1219 feared on the accrual side and has *zero*
  parity surface**, but its one genuinely unresolved piece — the class→path association — does **not** fall out
  of the kit as hoped. It also remains gated on content that is itself blocked (see [§6](#6-the-roster-conflict-blocks-both-gates)).

---

## 1. What changed since #1219 was written

| #1219 premise | Current state |
| --- | --- |
| "nothing reads live state like a current-health ratio mid-tick" | **Stale.** Cull's execute bonus samples the *target's* live missing-health fraction once per fire and folds it into that fire's damage multiplier — `BattleContext.cs:316-320`, mirrored in `battle-step.ts:57-58`. |
| "today modifiers are snapshot-fixed plus timed skill effects" | **Half stale.** Timed effects already mutate the live `AttributeCollection` mid-battle (`BattlerEffects.Apply`/`Advance`, with a `MaxHealth` re-clamp on both), so mid-battle attribute mutation is routine. What is genuinely absent is a modifier that is a *continuous function of live state*. |
| "in-class bonus … re-couples class into the just-tuned proficiency XP accrual / attribute pipelines" | **Overstated on the plumbing; partly conceded on the tuning.** The wiring is free: the accrual is a pure `Game.Core` function taking a `ProficiencyCatalog` (#1602), its live adapter (`ProficiencyRewardService.AccrueAndApply`) already receives the `Player`, and the offline simulator already carries `ResolveClass` + `BattleSnapshot.ClassId` — the class is in hand at every call site. The *balance* half of the worry is real though smaller than stated: see §2's `ratingDenominator` caveat for the residual coupling. |
| "an in-class bonus … changes efficiency, not access" | **Confirmed** — and stronger than assumed, see §2. |

---

## 2. Direction 1 — in-class progression bonus

### There is no shared pie, so a bonus really is a bonus

#1219's core worry is that an in-class bonus must not become an out-of-class tax. **The accrual model already
guarantees that**: `ProficiencyXpCalculator.Split` gives each path `pie × activity ÷ ratingDenominator`
*independently* — "the claims overlap and need **not** sum to 1, because there is no shared pie to split"
(`ProficiencyXpCalculator.cs:6-10`; the same statement appears in `game-design.md` → Classes). Multiplying one
path's claim therefore cannot reduce any other path's claim. The "bonus, never a penalty" framing is
mechanically achievable, not just an authoring convention.

**One honest caveat.** `ratingDenominator` is `max(playerRating, enemyRating)`. In-class levels pay attribute
bonuses, which raise `playerRating`, which — *while the player outweighs the enemy* — shrinks every path's
claim. So faster in-class progress does marginally slow out-of-class progress at second order. This is not
specific to the feature: it is the treadmill normalization every power gain (gear, levels, mods) already feeds.
Worth stating explicitly so it isn't rediscovered as a bug during tuning.

### Where it hooks, and what it costs

- **One multiplier on the slice.** `pie × activity ÷ denominator × inClassBonus`, applied per-path inside
  `ProficiencyAccrual.Accrue` (or threaded into `Split`). Boosting the pie for in-class paths is the same
  arithmetic — there is no meaningful choice between the two.
- **Zero parity surface.** Proficiency accrual is server-side only; the tallies it consumes are documented as
  "backend-only side channels — no parity surface" (`BattleContext.cs:355`), and `CombatRating` is explicitly
  server-only. Nothing in this direction touches the frontend simulator.
- **Both call sites already hold the class.** `ProficiencyRewardService.AccrueAndApply(..., Player player, ...)`
  and `OfflineProgressSimulator` (via `OfflineSimulationParameters.ResolveClass` + `BattleSnapshot.ClassId`).
- **Invariant safe.** It changes rate, never reachability — #982 decision 2 and #1126 decision 7 both hold.

### The real open question: where does "in-class" come from?

#1219 offers two options — derive from the kit, or author a bias list. **The kit-derived option does not work
as stated**, and that is the substantive finding here:

- **Offense keys are derivable.** A skill's `damagePortions` give damage types → `DamageTypes.Applies(type)` →
  `ActivityKeys.ForDamageKey`, so "the Wizard's Fireball trains Fire/Elemental" falls out statically.
- **Archetype and event keys are not.** `Parry`, `Crit`, `Dodge`, `Hex`, `Momentum`, `Sunder`, `Cull`,
  `Cadence`, `Heal` and `Reflect` are booked from what *happened* in the battle, not from a skill's static
  shape. The Swordsman's parry stance is the exact counterexample content-design already names: it is a plain
  skill whose only relevant property is a timed self-effect granting `ParryChance`. Nothing about it statically
  says "this trains Riposte" — the path is trained by counter damage that only exists once a parry lands.
- A skill-effect→activity-key map (`ParryChance` → `Parry`, `DodgeChance` → `Dodge`, …) would recover most of
  it, but it is a second source of truth for a binding the `Path.ActivityKey` field already owns, and it is
  ambiguous for the multiplier attributes.

**Recommendation:** an explicit authored list on `Class` (bias-only, e.g. `InClassPathIds`) — one reference-data
field, one Workbench section, one Content Health lint rule. Pair it with a *warning*-level lint when a biased
path is not plausibly reachable from the kit, so the explicit list can't silently drift away from the kit it is
meant to describe. This is the option #1126 decision 7 already anticipated ("would need a class→path
association, which this kit-derived model otherwise avoids").

### Gate

#1219's gate — *"reconsider once there is authored proficiency content (#1127) **and** a V1 class roster"* —
**still holds and is still unmet**: `content/paths.json` and `content/proficiencies.json` are empty, and
`content/classes.json` holds only the placeholder `Adventurer`. Tuning a bonus with nothing to tune against
remains premature. See [§6](#6-the-roster-conflict-blocks-both-gates) for why that content is itself stuck.

---

## 3. Direction 2 — conditional / "stance" passives

### The class seam is already built; what is missing is engine *channels*

A class's signature passive resolves to exactly one `AttributeModifier` tagged
`EAttributeModifierSource.Class` (`ClassSignaturePassive.GetModifier`, mirrored bit-for-bit in
`class-modifiers.ts`). So the question "can a class have a conditional passive?" reduces to "does an attribute
channel exist whose *consumption* is conditional?" — and Cull proves the answer is yes and cheap:
`ExecuteBonus` is a base-0, authored-only attribute read at the damage site against live target health.

Taking #1219's three examples in turn:

| #1219 example | What it actually needs |
| --- | --- |
| **rage** — damage rises as own health drops | **One Cull-shaped multiplier.** A base-0 authored-only attribute read in `ResolvePlayerHit` against the *attacker's* own missing-health fraction, instead of the target's. Sampled once per fire, exactly like the execute multiplier; ~2 lines per simulator plus parity vectors. |
| **escalation** — skills strengthen the longer the fight runs | **Nothing — already authorable.** This is the **Momentum** delivery archetype (#1428), already implemented: a skill self-applies a timed amplification ramp, tracked via `BattlerEffects`' `tracksMomentum` contribution. A refreshing self-buff on the class's secondary *is* escalation. |
| **first strike** — opening hits crit | **Not a class feature.** It needs per-battler hit-count bookkeeping, which is already tracked as a deferred **skill-effect** extension in [#336](https://github.com/ginderjeremiah/GameServer/issues/336) ("hit-count durations & every-Nth-hit triggers"). Build it there; a class kit then uses it. |

**So direction 2 as scoped — "conditional passives are a class feature needing net-new parity-sensitive
battle-sim logic" — largely dissolves.** One example is content today, one belongs to an existing tracked
extension, and one is a small engine channel that is not class-specific in any way (an item or skill could
grant `RageBonus` just as well as a class passive could).

### Cost taxonomy, cheapest first

For whatever conditional channels do get built, the shape matters far more than the trigger:

1. **Sampled scalar (Cull template).** A live quantity read once per fire and folded into that fire's
   multiplier. Precedent exists on both sides; no new state; parity is a fixed sampling point.
2. **Elapsed-time ramp.** Already expressible as a self-applied timed effect (Momentum). No new state.
3. **Hit-count trigger.** New per-battler counters on both simulators, plus new parity vectors — #336's item.
4. **Threshold toggle** (a stance that switches on below 30% health). **Most expensive and worth avoiding:**
   both simulators must toggle on the *same tick*, and a toggle that applies/expires a modifier interacts with
   `BattlerEffects`' shared-expiry stacking and the `MaxHealth` re-clamp. Shapes 1–2 buy the same fantasy
   without a discrete state machine.

### Two constraints any new channel must satisfy

- **The commitment rule** (`content-design.md` §4): a channel earns a proficiency path only if it is *committed*
  — an opt-in enabler with a real opportunity cost, base 0 when uncommitted, multiplicative Technique path, and
  a tallied magnitude. A new offense channel added with no tally and no activity key is universal free value
  that trains nothing. **Open question:** a signature passive is free *within* its class (no opportunity cost
  paid), so does a class passive granting an archetype enabler satisfy the rule, or does the rule need an
  explicit carve-out for authored-only class enablers? The defensive-mechanics spike
  ([`1331-defensive-mechanics.md`](./1331-defensive-mechanics.md), in review as
  [PR #2401](https://github.com/ginderjeremiah/GameServer/pull/2401)) raises the identical question for a shield
  pool; the two should be answered together.
- **Rating classification — the structure reuses, the constant does not.**
  `ECombatRatingClassification` is exhaustiveness-tested (#1526 decision 9,
  `CombatRatingTests.Classify_IsDefinedForEveryAttribute`), so a new attribute fails the build until it is
  priced. A player-only conditional channel slots into `AsymmetryGated` alongside crit/dodge/parry/execute, and
  the *shape* of Cull's term (a reference constant × the attribute) carries over. **Its constant does not.**
  Cull is priced `1 + ExecuteBonus × RefMissingHealthFraction` with `RefMissingHealthFraction = 0.5` justified
  by "health depletes from full to empty over the reference fight" (`ServerGameConstants.cs:122-129`) — a
  derivation that holds only because the sampled entity is *the one that dies*. A self-health channel samples
  the rated player, who does not deplete to empty in a won reference fight, so the constant must be re-derived
  rather than inherited. Worse, the player's average missing health is an inverse function of the survivability
  term `Rate = √(Offense × Survivability)` already computes, so a flat constant systematically over-values rage
  on tanky builds and under-values it on fragile ones — and because the rating feeds `ratingDenominator`, that
  misvaluation lands back on accrual rates. See [§5](#5-overlap-with-1331): this is a named deliverable of the
  shared design pass, not something to inherit by assumption from whichever half lands first.

---

## 4. Directions the issue didn't list

Per the ideation brief, exploring past the two listed directions:

- **Class-tailored challenges are the highest-value identity lever, cost no engine work, and are unblocked
  today.** `content-design.md` §5 already designs a per-class progression route built out of challenges keyed on
  the class's natural activity ("kill N enemies with sword skills"), class-*flavoured* but never class-locked.
  That delivers "my class plays differently" through content alone. The kills-by-damage-type statistic those
  routes depend on ([#1455](https://github.com/ginderjeremiah/GameServer/issues/1455)) **shipped on 2026-07-02**
  — `EStatisticType.KillsByDamageType`, `EChallengeType.KillsByDamageType`, the recorder, the `DamageType`
  breakdown axis, admin validation and the Workbench UI are all in the tree — so this is pure Workbench
  authoring, available now. **If the goal is felt class identity soonest, this outranks both of #1219's items,
  and unlike them it waits on nothing.** (`content-design.md` §5 still carried a "⚠ assumes a statistic that
  does not exist yet" warning on those routes; it went stale when #1455 shipped and is corrected in this change.)
- **Signature-passive expressiveness is capped at one modifier.** `ClassSignaturePassive` grants exactly one
  attribute. Before adding conditional channels, it is worth asking whether a class wants *two* passive terms
  (e.g. a fingerprint nudge plus an archetype enabler) — a smaller, purely additive change to the same seam.
- **Word-of-power depth.** Classes render a flat decorative label; proficiencies decipher theirs as they level.
  A class label that deciphers on account/character milestones is pure cosmetic identity with no balance surface.
- **Explicitly still rejected:** class-locked nodes (#982 decision 2), class-gated gear — and the soft
  alternative is no longer hypothetical, since the proficiency gear-gate
  ([#1124](https://github.com/ginderjeremiah/GameServer/issues/1124)) shipped on 2026-06-27, so
  "heavy armour requires the martial proficiency" is authorable today — and any out-of-class *tax* (fights the
  multi-character design). Nothing found here reopens those.

---

## 5. Overlap with #1331

**Rage (this issue) and last-stand (#1331) are the offense and defence halves of one mechanic** — both read the
player's own live health fraction. The sibling spike [`1331-defensive-mechanics.md`](./1331-defensive-mechanics.md)
(in review as [PR #2401](https://github.com/ginderjeremiah/GameServer/pull/2401)) reaches the same conclusion
from the other side (it cites #1219's now-stale "nothing reads live state" line as grounds to revise this
issue's cost estimate down).

They should be **designed as one channel family, not twice**, and that shared pass owes three decisions:

1. **Sampling** — where in the fire the player's own health fraction is read (Cull's once-per-fire sampling
   point is the obvious template, and it *does* transfer).
2. **Pricing in `CombatRating`** — the reference constant must be re-derived rather than inherited from Cull's
   `RefMissingHealthFraction`, and its build-correlation with the survivability term resolved (see
   [§3](#two-constraints-any-new-channel-must-satisfy)). This is the decision most at risk of being made
   silently by whichever half ships first.
3. **Tally + activity key** — what a self-health channel books, and whether both halves share one path or take
   two, under the commitment rule.

Only then the two authored attributes on top. Whichever lands first should set the template *explicitly*, not
by precedent.

---

## 6. The roster conflict blocks both gates

#1219's examples name **Warrior / Mage / Ranger** — the strawman from #1126 §H. `content-design.md` §5 instead
specifies **Swordsman / Bowman / Wizard / Knight**, and that is the roster #1457/#1492 already assume by name.
This is the same unresolved conflict flagged on [#1127](https://github.com/ginderjeremiah/GameServer/issues/1127)
and [#1492](https://github.com/ginderjeremiah/GameServer/issues/1492), which is currently blocking the
proficiency-tree and class-roster authoring — i.e. **exactly the content both of #1219's gates wait on**.

Nothing in V2 can be tuned until that is settled, so it is on the critical path for this issue too. It is also
purely a call for the owner: both rosters are coherent, they just disagree.

---

## 7. Open questions

1. **Direction 2's framing.** Do you accept that conditional passives dissolve into (a) an authored self-health
   channel shared with #1331, (b) Momentum for escalation, and (c) #336's hit-count triggers — leaving no
   class-specific runtime work? If so, #1219 direction 2 should be **closed into those three**, and the class
   half becomes content authoring.
2. **The commitment-rule carve-out.** Does a class signature passive granting an archetype enabler count as
   "committed" (it is free within the class), or does it need an explicit exemption? Same question
   [`1331-defensive-mechanics.md`](./1331-defensive-mechanics.md) raises for the shield — please answer them
   together.
3. **Direction 1's association.** Authored bias list on `Class` (recommended) or a skill-effect→activity-key
   derivation? The kit-derived option in the issue only covers offense keys and misses every archetype path.
4. **Direction 1's timing.** Keep it gated behind authored content, or is a strawman multiplier (~+20%) with no
   content to tune against still worth landing early to de-risk the seam?
5. **The roster.** Swordsman/Bowman/Wizard/Knight (content-design.md, and what shipped code already names) or
   Warrior/Mage/Ranger (#1126/#1219)? This blocks #1127, #1226, #1492 and both gates here.
6. **Priority check — and it is not a sequencing question.** The §4 tailored-challenge routes are **available
   right now** (#1455 shipped; they are pure Workbench authoring), while **both** V2 directions are gated on the
   unauthored roster in §6. So: spend the next class-identity slice on the routes, which can start today, or
   hold it for a V2 direction that cannot start until Q5 is answered?

**No implementation issues have been created yet** — per the ideation rule, splitting waits on the answers above.
