# Spike #1526 — Combat-rating power measure: replace SumCoreAttributes with a capability rating

- **Spike issue:** [#1526](https://github.com/ginderjeremiah/GameServer/issues/1526)
- **Status:** Ideation complete; design decided. Implementation split into sub-issues (see [Implementation issues](#implementation-issues)).
- **Related:** follow-on from [#1426](./1426-attribute-identity-sourcing.md) (Decision 6); reshapes the reward curve shipped with the effect-based accrual ([#1318](./1318-proficiency-progression-avenues.md)); the rating's marginals feed the enemy dead-stat lint ([#1529](https://github.com/ginderjeremiah/GameServer/issues/1529)).

## The question, as filed

`DefeatRewards.SumCoreAttributes` counts *investment* (sum of additive core-attribute modifiers) where its two consumers — the XP difficulty band and the proficiency accrual denominator — want *capability*: player and enemy don't scale equally per point, dead stats are taxed, authored secondaries and multiplicatives are invisible. Seed direction: a combat rating from the assembled battle snapshot.

## What research sharpened

- **The defect is slightly worse than filed**: the enemy's attribute sum is not just the band input but the **XP magnitude itself** (`ExpReward = enemyAttTotal × multiplier`), so swapping the measure rescales all payouts, not just band placement.
- **`PlayerPower` never reaches the client** (server-side only; the API DTO omits it), so there was no display-meaning migration — instead the rating becomes a deliberate UI feature (Decision 7).
- **Sequencing is independent of the #1426 severing issues** (#1523/#1524/#1525): the rating prices whatever the engine currently reads (e.g. enemy `CooldownRecovery` fires today and is priced; enemy `CriticalDamage` never fires and is neutral), and the opt-in pairs go dormant-neutral automatically once severed.
- **The discussion exposed a second, co-equal defect in the reward curve itself** (Decision 4): under the `ratio²` band, an *honest* measure makes build optimization lower XP/hour — the old measure's unpriced channels were the only thing masking it.

## Decisions

Settled with the project owner during the spike:

1. **The rating: `√(OffenseRate × Survivability)` vs fixed reference profiles, both sides through one pure function** over the assembled battler (attributes + fielded skills + counter skill). The geometric mean is exact, not aesthetic: `(enemyRating/playerRating)²` **is the time-to-kill ratio** (time to kill the enemy ÷ time to be killed) — the outcome-relevant quantity in this engine. Offense = Σ fielded skills' `expectedHit ÷ effectiveCooldown` (per-portion amplification, crit expectation, execute at mean missing-HP ½, DoT effects at their exact steady-state `magnitude × duration ÷ cooldown` closed form) plus reflect and riposte terms; survivability = `(MaxHealth + regen × refDuration) ÷ mitigated-and-avoided fraction` (the regen-credit form deliberately avoids the outheal-everything pole). Server-only, **no parity surface**; the rating reuses the `Battler` read methods the engine itself uses (anti-drift). Fixed reference profiles keep the measure *intrinsic* — matchup tech (right resist vs this enemy) is deliberately not taxed per-fight.

2. **Attribute-modifier effects are priced from v1, steady-state.** Buff/debuff kits must not read weaker than they are (an under-rated player inflates every reward), so every fielded effect applies to a copy of the attribute state at its uptime-weighted average magnitude — `amount × min(duration, refDuration/2) ÷ cooldown`, one closed form covering both the `d ≤ cd` uptime case and the shared-expiry ramp (`d > cd`, Momentum-style) — then offense/survivability compute once from the averaged state (order-free, no simulation). Opponent-targeted debuffs (Hex, Sunder) apply against the reference defense; the engine's own below-zero semantics (negative resistance = vulnerability, negative Toughness amplifies) make the formulas hold. A **nonzero `RefToughness`** both makes Sunder meaningful and prices DoT's real Toughness-bypass premium.

3. **Resistances price against a uniform reference incoming type mix** (not pure physical, not per-matchup): resist investment counts at its average value across content. An off-type resist point counts slightly even when useless this fight — accepted as inherent to an intrinsic measure, preferred over leaving resists unpriced.

4. **The reward curve is reshaped: enemy-authored bounty, no upward premium** — `XP/kill = k × enemyRating × min(r, 1)²` with `r = enemyRating ÷ playerRating`. The old curve (flat ±20% band, `ratio²` premium above, 4× cap) paid a premium linear in your time-to-kill, so *killing faster linearly cut per-kill pay* while the fixed inter-battle cooldown ate a growing share of each battle slot: optimizing a build against a fixed enemy **lowered XP/hour** across most of the curve, with perverse cliffs at both band edges (the spot just inside the top edge is a local minimum). The measure swap makes that gradient universal (the old escape hatch was optimizing through unpriced channels — exactly the defect being fixed), so the curve had to change with it. Under the bounty curve: XP/hour peaks at matched fights; optimizing at-or-above-matched strictly raises XP/hour (same bounty, faster kills); anti-grind below matched is preserved; punching up still pays best (bigger bounty) but saturates instead of jackpotting; and **sandbagging dies structurally** — nothing pays more for being weak, so the rating no longer needs to be exploit-proof, only accurate. The band, its cap, and `MaxExpRewardMultiplier`'s XP role dissolve; `MaxExpPerGrant` remains as the tamper backstop. The *underdog premium* (the same kill paying a weak player up to 4×) is deliberately dropped — per-hour fairness beats per-kill drama in an idle game; a David-vs-Goliath moment bonus, if ever wanted, returns as a bounded one-off, not as the curve's shape.

5. **Proficiency accrual mirrors it: `XP_path = pie × activity ÷ max(playerRating, enemyRating)`.** Above your weight the claim rate is what a matched player would earn; at or below, the #1318 treadmill is unchanged. The 4.0 clamp retires (`activity ≤ enemyHP` bounds claims naturally). Verified corner: a wall enemy isn't a per-hour training exploit — activity/hour reduces to the player's DPS regardless of enemy sponginess.

6. **The enemy is rated on its fielded loadout** (`BattleSkills`, the ≤4-skill draw actually fought), not its authored pool — the measure prices the fight that happened, and the per-encounter level roll already varies XP. Offline: the player's rating computes once per stationary away window; enemy ratings per encounter (closed-form arithmetic, cheap).

7. **The rating is surfaced in the UI for both battlers** (display-only; the server sends values, the client never recomputes — no parity surface). Player power on the attributes screen gives #1528's inert-stat signaling a numeric companion (an enabler-less point visibly doesn't move the number); enemy power on the battle screen makes the anti-grind curve legible ("you've outgrown this zone").

8. **Engine asymmetry is mirrored, not the attribute sheet**: enemies don't crit/dodge/parry, so their rating skips those terms even where a designer authors a crit chance — otherwise the dead-LUK defect is recreated one level up. The asymmetry lives in one documented place and switches on when enemy proc parity (#1426 Decision 7c) lands.

9. **The maintenance contract is enforced, not hoped for**: an explicit classification map over `EAttribute` (offense / survivability / both / neutral-by-design / asymmetry-gated) with an exhaustiveness test — a new attribute fails the build until its rating term is decided. The rating's per-attribute finite-difference marginal doubles as the dead-stat detector for the #1529 enemy content lint.

10. **Tuning surface**: reference constants (`RefToughness`, `RefIncomingTypeMix`, `RefAttackRate`, `RefDps`, `RefFightDuration`) live in `ServerGameConstants` as strawmen; the XP scale `k` and the `ProficiencyXpPerVictory` retune are chosen by a calibration report over authored content and reference builds (old-vs-new per enemy, zone `r`-placement, XP/hour and training/hour curves across the arc).

## Alternatives weighed (recorded in #1426 — not re-treaded)

Per-attribute weight tables (meta-chasing), battle-outcome-derived difficulty (pays for sandbagged/slow fights), level-based power (ignores build). Newly rejected here: **keeping any upward `rⁿ` premium** on an honest measure — every exponent > 0 leaves a "look weaker than you are" incentive whose size is the rating's model error, and the quadratic additionally inverts the optimization gradient (Decision 4's math).

## Implementation issues

Created as sub-issues of #1526:

| Issue | Area | Scope | Summary |
| --- | --- | --- | --- |
| [#1531](https://github.com/ginderjeremiah/GameServer/issues/1531) — combat rating engine | engine (server-only) | large | Decisions 1–3, 8–9 — the pure rating function, reference constants, exhaustiveness test, marginal helper |
| [#1532](https://github.com/ginderjeremiah/GameServer/issues/1532) — consumer swap + curve reshape | rewards / proficiency | medium | Decisions 4–6 — bounty curve, max-normalization, threading, test + doc rewrites; depends on #1531/#1533 |
| [#1533](https://github.com/ginderjeremiah/GameServer/issues/1533) — calibration report | content tooling | medium | Decision 10 — old-vs-new pricing, zone placement, per-hour curves; picks `k` and pie |
| [#1534](https://github.com/ginderjeremiah/GameServer/issues/1534) — power ratings in UI | frontend + payloads | medium | Decision 7 — display both ratings; depends on #1531/#1532 |

## Documentation to update on landing

- **[game-design.md](../game-design.md)** — *Experience Rewards* (band → bounty curve) and the *Proficiency XP* power definition/normalization; **[content-design.md](../content-design.md)** §3 difficulty & reward guidance; **[backend-attributes.md](../backend-attributes.md)** signature-passive power paragraph. _(All ride [#1532](https://github.com/ginderjeremiah/GameServer/issues/1532), not done here.)_

## Out of scope / deferred

- **Enemy proc parity** — when the recorded #1426 seam lands, the asymmetry-gated terms switch on; no rating redesign needed.
- **Underdog / first-clear bonus** — possible bounded one-off if punch-up moments need more drama; deliberately not part of the curve.
- **Per-matchup rating variants** — the measure stays intrinsic by design; matchup tech paying off unpriced is the hidden-synergy pillar expressed in rewards.
