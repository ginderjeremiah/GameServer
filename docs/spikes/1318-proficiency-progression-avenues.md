# Spike #1318 — Other avenues for progressing proficiencies (effect-based accrual)

- **Spike issue:** [#1318](https://github.com/ginderjeremiah/GameServer/issues/1318)
- **Status:** Design decided (ideation complete); implementation split into native sub-issues (see [Implementation issues](#changes-by-area--implementation-issues)). Gated on the damage-types pipeline ([#1320](https://github.com/ginderjeremiah/GameServer/issues/1320)). All implementation issues have landed: engine + offense routing (#1336), the remaining avenues (#1338/#1339), weapon leaves (#1340), and admin authoring (#1341).
- **Related:** reworks the shipped proficiency accrual ([#982](https://github.com/ginderjeremiah/GameServer/issues/982) — #1116 / #1160 / #1161); depends on damage types ([#1320](https://github.com/ginderjeremiah/GameServer/issues/1320), foundation shipped) and the mitigation rework ([#1330](https://github.com/ginderjeremiah/GameServer/issues/1330), reflection); spins off two follow-up spikes ([#1342](https://github.com/ginderjeremiah/GameServer/issues/1342), [#1343](https://github.com/ginderjeremiah/GameServer/issues/1343)).

## Goal

The shipped proficiency system derives XP purely from skill **representation** — a path earns a slice of a fixed pie if one of its contributing skills *fired* in a won battle, split by `attention (tierWeight × contributionWeight) × falloff`. That ties every proficiency to "a skill you cast", so it can't express masteries that are **defensive** (elemental/DoT resistance), **reactive** (crit/dodge), or otherwise not a damage-dealing skill — the avenues the issue asks for.

The conclusion of this spike is that the right answer is **not to bolt extra avenues onto the representation model, but to replace it.** There is one avenue, and "skills fired" was only ever a special case of it.

### The framing that drove the design

> **Every proficiency is a mastery track for an _activity key_, trained by `pie × (that activity ÷ your total attributes)` on victory.** Offense, elemental/DoT, resistance, crit, dodge, healing, and reflection are all instances of one formula and one routing rule; the issue's three examples stop being special cases.

Two properties of the shipped model are deliberately **kept**: XP is computed **server-side at battle completion, off the deterministic tick loop** (so it adds **no parity surface** — only the eventual attribute *bonus* is parity-sensitive), and it is **victory-only**.

## Current state (what already exists)

- **The proficiency system shipped** ([#982](https://github.com/ginderjeremiah/GameServer/issues/982)): paths/tiers, `PlayerProficiency` persistence, admin authoring, and the representation + absolute-falloff accrual. Today the pie is `ServerGameConstants.ProficiencyXpPerVictory` (`10.0`, **flat**) × `DefeatRewards.DifficultyMultiplier` (banded `ratio²`, `1.0` within ±20%), split by attention × falloff in `ProficiencyXpCalculator` / `ProficiencyRewardService.BuildContributions`, consuming `BattleStats.SkillStats` (which skills fired).
- **`BattleStats` already aggregates the output book** — per-skill `TotalDamage`/`Uses`, plus crit damage, dodged damage, blocked damage, healing, and (untyped, global) damage taken. **Damage taken is not typed and DoT is summed into untyped per-second accumulators** with no source attribution.
- **Damage types ([#1320](https://github.com/ginderjeremiah/GameServer/issues/1320)) foundation has shipped** — `EDamageType`, the amp/resist attributes, and the `applies(type) → keys` map. Direct-hit typing and the per-type DoT split are in flight. The taxonomy is extensible by design ("200+ values architecturally fine"), and a "category" is just a key several leaves' `applies()` sets include (e.g. `Burn` pulls `Fire`).
- **Proficiency bonuses are already free** — `EAttributeModifierSource.Proficiency` exists, so a path can grant any attribute (including #1320's type-resist) at a milestone. The issue's ask is the **XP source**, not the payout.
- **`DefeatRewards.SumCoreAttributes`** already measures a battler's power (sum of core additive modifiers) for the difficulty ratio — the same measure this spike normalizes by.

## Decisions

Settled with the project owner during the spike:

1. **Replace representation with effect-based accrual.** `XP = basePie × clamp(activity ÷ totalAttributes)`, **victory-only**, routed to the path's frontier tier. Computed at battle completion (live + offline), off the tick loop.

2. **Power-normalization subsumes the difficulty multiplier.** `activity / totalAttributes` *is* a continuous difficulty ratio (trivial enemies deal/take little relative to your power; over-level ones more), so the separate banded `DifficultyMultiplier` is dropped; the `clamp` mirrors `MaxExpRewardMultiplier`. **Do not stack both** — power would appear in the denominator twice and create a strip-power exploit.

3. **No representation floor.** A path earns strictly in proportion to its real contribution; **commitment (build + loadout) is the only lever**. A dabbled skill doing 2% of your damage trains its path at ~2% rate — denying the "slot a 4th skill for free milestones" anti-pattern. The "why is this slow" question is a **legibility** concern handled in UI (surface the contribution rate), not a mechanic.

4. **Independent fractional claims over two books.** A battle produces an **output book** (damage you dealt) and an **incoming book** (damage that came at you). Each proficiency claims `pie × (its activity ÷ power)` independently; claims **overlap and need not sum to 1**.
   - *Within* a book, output damage partitions by type, so **focus beats spread** (4 fire skills train Fire fully; 1-of-4 trains it at a quarter).
   - *Across* axes there is **no dilution** — offense, defense, and proc train in parallel, because they are different axes. The opportunity cost lives in **build investment + per-tier curves**, not a shared pie. Consequence (intended): a battle mints **variable** total proficiency XP — more for a multi-axis build.

5. **Magnitude is carried by power-normalization, consistently.** This is what makes defense respond to player choices at all (a pure type-share would be set only by the enemy). Normalize by **total attributes** (not MaxHP): a point of Endurance and a point of Strength raise the bar equally, so durability is not singled out, exposure is **pre-mitigation** (no self-throttle), and growing stronger naturally outgrows old threats (a treadmill, with anti-grind for free).

6. **Routing by `ActivityKey`, not skill→path.** Each path declares a single `ActivityKey` (a damage type, a category, or a combat event). A battle quantity trains the path(s) its key resolves to via the (extended) `applies()` map. The `SkillPathContribution` join, `HomeTier`, and `FalloffBase` are **removed**.

7. **Remove skill falloff.** Under damage/power a stale skill already contributes proportionally less (automatic anti-staleness), so falloff is redundant double-discounting; it contradicts "train what you do"; it **complements seed-skill removal** (your existing typed damage fully trains a freshly-opened tier — no wall, no seed); and it cannot apply to DoT cleanly. The per-tier XP **curves** own pacing instead.

8. **Drop rarity/tier weight from accrual.** Damage already reflects a skill's power; an explicit rarity multiplier double-counts. Rarity reverts to display-only.

9. **Activity keys span both books.**
   - **Output:** per-type damage dealt (elemental + weapon leaves + categories), crit damage, healing done, reflected damage.
   - **Incoming:** per-type **pre-mitigation** exposure, dodged damage.

10. **Weapon types as damage-type leaves.** Martial masteries (Swordsmanship, Axe mastery, …) become ordinary type-routed paths, parallel to elemental masteries. **Static per-skill typing** — a martial skill is authored with a fixed weapon type (preserves art/animation/identity coherence; rejected: inherit-from-equipped-weapon, which forces every skill to make sense for every weapon). Weapon leaves sit under the `Physical` **category key** (`applies()` = `[<weapon>, Physical]`); existing `Physical` skills are unchanged (`Physical` does double duty as the generic-physical leaf and the shared key). Generic `Physical` routes to a broad **Martial** path; weapon leaves route to their weapon path **plus** Martial. **Per-weapon resistance is not implemented** — weapon leaves may carry amplification only, and a weapon hit resolves to `PhysicalResistance` via the shared key, so physical resistance stays the only physical mitigation lever. Purely **additive** on the shipped #1320 foundation (no migration). Needs an `Unarmed` default.

11. **Crit/dodge stay damage-type-neutral.** Single global **Precision** (crit damage) and **Evasion** (dodged damage) tracks; crit damage is not typed at the recording site, so per-type proc tracks would cost crit-attribution-by-type for a thin payoff. Block is gone (#1330), so it is not an avenue.

12. **Healing and reflection are in scope** as activity keys — **Restoration** ← healing done, **Retribution** ← reflected damage (reflection depends on #1330). The model covers any **combat magnitude**; **pure-utility** masteries (cooldown, buff duration, resource gen — no magnitude) are out, delivered as **milestone bonuses on damage-trained paths**. Cross-path **gateways** (e.g. lava = adv-fire + adv-earth) need their own activity key + content to be trainable now that seeds are gone.

13. **Multi-typed damage is forward-compatible but deferred** ([#1343](https://github.com/ginderjeremiah/GameServer/issues/1343)). Accrual consumes per-type damage *portions*, so single-type works now and multi-type drops in later — the split ratio becoming the cross-school contribution weight. This is why **no separate per-skill contribution weight is reintroduced**.

## The accrual model

```
XP_proficiency = basePie × clamp( proficiencyActivity ÷ totalAttributes )   // victory-only → frontier tier
   offense_fire  = pie × fireDamageDealt   / power     // output book; weapon leaves identical
   precision     = pie × critDamage        / power     // output book (event)
   restoration   = pie × healingDone       / power     // output book (event)
   retribution   = pie × reflectedDamage   / power     // output book (event, #1330)
   resist_fire   = pie × fireExposure      / power     // incoming book, exposure is PRE-mitigation
   evasion       = pie × dodgedDamage      / power     // incoming book (event)
```

`power = SumCoreAttributes` (the difficulty system's measure). The clamp mirrors `MaxExpRewardMultiplier`. Properties: magnitude everywhere; focus beats spread within a book; axes train in parallel; no self-throttle (a resist isn't a core attribute and exposure is pre-mitigation); anti-grind + treadmill from power-normalization; victory-only closes the loss-farming exploit; offline replays identically (same loadout + matchups → same activity/power).

### Routing

A path carries one `ActivityKey`; routing a quantity of type `T` runs `applies(T)` and trains the path bound to each resulting key (so a fire hit trains the `Fire` path **and** the `Elemental` path; a sword hit trains the weapon path **and** `Martial`). Events (crit/dodge/heal/reflect) and incoming exposure resolve to their keys the same way. One `ActivityKey` field per path replaces the entire skill→path join.

## Changes by area / Implementation issues

Created as native sub-issues of #1318:

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| [#1336](https://github.com/ginderjeremiah/GameServer/issues/1336) — Accrual rework: effect-based `pie × activity/power` + `ActivityKey` routing (live + offline) | engine | #1320 direct-hit typing | large |
| [#1337](https://github.com/ginderjeremiah/GameServer/issues/1337) — `BattleStats` additions: typed damage-dealt, pre-mitigation exposure, power snapshot | tracking | #1320 direct-hit typing | medium |
| [#1338](https://github.com/ginderjeremiah/GameServer/issues/1338) — Resist & DoT-dealt activity bindings (incoming book + typed DoT) | bindings | #1337, #1320 DoT split | medium |
| [#1339](https://github.com/ginderjeremiah/GameServer/issues/1339) — Proc / heal / reflect activity bindings | bindings | #1336 (Retribution: #1330) | medium |
| [#1340](https://github.com/ginderjeremiah/GameServer/issues/1340) — Weapon-type damage leaves (`Physical` category) for weapon masteries | taxonomy | shipped #1320 foundation | medium |
| [#1341](https://github.com/ginderjeremiah/GameServer/issues/1341) — Admin authoring (`ActivityKey`) + docs update | tooling/docs | #1336, #1340 | medium |

Shape: **#1336** is the engine; **#1337** feeds its offense and the incoming-book bindings; **#1338**/**#1339** are the new avenues (gated on #1320 DoT split and #1330 respectively); **#1340** is the additive weapon taxonomy; **#1341** is the authoring + documentation surface. No issue adds a frontend↔backend parity vector (accrual is off the tick loop); each carries unit/integration tests.

## Documentation to update on landing

- `docs/game-design.md` — rewrite the **Proficiency XP** section for the effect-based `pie × activity/power` accrual, `ActivityKey` routing, the activity-key set (offense / elemental / weapon / DoT / resist / crit / dodge / heal / reflect), the two-book model, and the removal of falloff / home-tier / rarity-weight. Touch the **Damage types** section for the weapon leaves under the `Physical` category. (With #1336, #1340, #1341.)
- `docs/backend.md` / `docs/backend-battle.md` — the at-completion XP hook now reading typed `BattleStats` aggregates and normalizing by power; note it remains off the tick loop. (With #1336, #1337.)

## Out of scope / deferred

Tracked standalone (closing this spike does not depend on them):

- **Restrict skill usability by equipped weapon** ([#1342](https://github.com/ginderjeremiah/GameServer/issues/1342), spike) — surfaced by static weapon typing; decides whether a weapon-typed skill is gated on the matching weapon, and pins down the skill ↔ weapon ↔ loadout relationship. Not a blocker (static typing works without gating).
- **Multi-typed damage** ([#1343](https://github.com/ginderjeremiah/GameServer/issues/1343), spike) — multiple damage types per skill with split ratios; the home for cross-school skills (and the cross-school masteries that fall out for free). Forward-compatible with this spike's accrual.
- **Per-weapon resistance** — weapon leaves ship amplification-only; per-weapon resist is an opt-in type lever to add later if encounter design wants weapon counters.
- **Pure-utility masteries** — no combat magnitude to key on; delivered as milestone bonuses on damage-trained paths rather than their own paths.
