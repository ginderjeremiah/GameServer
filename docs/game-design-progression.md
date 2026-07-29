# Progression Design — Statistics, Challenges & Zone Gating

This doc owns the per-statistic and challenge-gating design detail split out of [game-design.md → Challenges and Statistics](./game-design.md#challenges-and-statistics) — the outcome taxonomy, goal-comparison semantics, the clear-vs-farm split, kill attribution, and zone unlock gating. Start with the main doc for the high-level model; this file holds the per-feature decisions.

## Battle Outcomes (`BattlesWon` / `BattlesLost` / `BattlesAbandoned`)

Every completed battle is classified by its **outcome**, and the three outcomes are mutually exclusive so they partition all battles:

- **`BattlesWon`** — the enemy died (the player won).
- **`BattlesLost`** — the player died.
- **`BattlesAbandoned`** — neither combatant died. This covers the player walking away from an unfinished fight (e.g. retreating from a boss or switching zones mid-battle) **and** the 2-minute battle timeout being reached in a stalemate (a **draw**) — both resolve as "neither died", carry no rewards, and are deliberately **not** counted as a loss since the player never actually fell (#202, #886).

The classification keys off the simulated outcome (`victory` / `playerDied`), not which code path ended the battle — a battle abandoned via `AbandonBattle` in which the player had in fact already died is still a loss, and a timeout that neither side could have survived past the cap is an abandon (a draw), not a loss. All three are recorded both globally and per-enemy. Like `BattlesLost`, `BattlesAbandoned` is a "lower is better" statistic on the Statistics screen.

The 2-minute cap is enforced as a real outcome on both sides: the live engine ends the fight as a draw at the cap (a boss draw drops back to the idle loop rather than re-spawning the boss), and the backend's abandon re-simulation clamps to the same cap so the replay resolves the reported stalemate identically rather than running past it into a spurious win/loss (see [backend-battle.md](./backend-battle.md#battle-setup-enemy-encounter)).

## Challenge Goal Comparison Direction

Most challenges are _accumulating_ goals: the tracked statistic increases over time and the challenge is completed once it reaches **at least** the goal (e.g. "defeat 100 enemies"). However, some statistics are minimized rather than maximized — for example, `FastestVictory` records the lowest victory time, where lower is better. A `TimeTrial` challenge ("win a battle within N seconds") is therefore satisfied when the tracked value is **at or below** the goal, which is the opposite comparison. For **at or below** goals, "no data yet" is the **absence of the statistic's row** (e.g. the player has not won a qualifying battle) and does **not** complete the challenge — otherwise a brand-new player with no victories would instantly satisfy every time trial. A stored value is always a genuine recording (`0` included), so a legitimately recorded `0` — e.g. an instant `FastestVictory` — **does** satisfy the goal (see the row-presence convention in [backend.md](./backend.md#player-facing-read-projections)).

## Zone Clears (`ZonesCleared`)

A zone is considered **cleared** when the player wins a battle started through the dedicated **"Challenge Boss"** action against that zone's single dedicated boss. The clear keys off an explicit dedicated-boss marker captured at battle start, **not** the enemy's `IsBoss` flag or the player's `CurrentZoneId` — so only the challenge path clears a zone, never a boss that happens to roll out of a random spawn table (the marker mechanics live in [backend-battle.md](./backend-battle.md#battle-setup-enemy-encounter)). (See the boss battle-setup decision in [backend-battle.md](./backend-battle.md#battle-setup-enemy-encounter) for the dedicated-boss model itself.)

A dedicated-boss victory updates two distinct statistics, which are deliberately kept separate so each answers a different question:

- **`ZonesCleared` counts _distinct zones ever cleared_, not boss-victory events.** The per-zone entry (keyed by zone id) is a binary **"cleared" flag** that flips from `0` to `1` on the zone's first clear and stays there; the global counter increments **only** on that `0 → 1` transition. Re-farming a zone's boss therefore never re-counts the zone — clearing two different zones once each gives a global `ZonesCleared` of 2, while clearing one zone's boss fifty times leaves it at 1. A `ZonesCleared` challenge can still target either "clear any zone" (global) or a specific zone (per-zone flag).
- **`BossesDefeated` is the _farm counter_.** It increments on **every** dedicated-boss victory, tracked both globally and **per-boss** (keyed by the boss enemy's id), so a challenge can target either "defeat any boss N times" or "defeat this specific boss N times".

This split keeps "how many zones have I beaten" and "how many times have I farmed this boss" as separate, individually meaningful numbers.

## Kills by Damage Type (`KillsByDamageType`)

Backs "kill N with `<type>`" challenges — e.g. *"kill 20 enemies with fire magic"* (#1455). A kill is attributed to whichever leaf `EDamageType` the player dealt the **most** damage of that battle (the offense book already accumulated for proficiency training), tie-broken on the lower enum ordinal — not the literal killing blow, which has no well-defined type on a multi-portion hit and would need new tracking through the tick loop for no real accuracy gain. This is a pure post-battle read (no battle-parity surface), robust to last-hit noise.

The kill rolls up through the same `Applies()` map proficiency training uses, so a Burn-majority kill books `Burn`, `Fire`, `Elemental`, and `Dot` alike — one kill counts toward every "kill with `<family>`" challenge it plausibly belongs to. Because weapon leaves are damage-type leaves ([game-design.md → Items](./game-design.md#items-item-mods-and-tags)), the same statistic also backs "kill with a weapon type" without separate plumbing. Unlike the other entity-scoped statistics there is **no global ("kills with any type") counter**, so a `KillsByDamageType` challenge always requires a target damage-type key (the content lint flags one that omits it).

## Zone Progression — locking zones behind challenges

Zone navigation is gated so later zones become real progression rather than being freely reachable from the start (#190). A zone carries an optional **`Zone.UnlockChallengeId`**: it is navigable iff that field is `null` (always open) **or** the player has completed the referenced challenge.

- **Gated on a challenge, not on `Zone.Order`.** A naive "zone `N` needs zone `N-1` cleared" chain bakes a rigid, contiguous linear order into the code and is awkward to evolve. Gating on a _challenge_ instead reuses the existing challenge/unlock system (the same machinery that unlocks items and mods), decouples gating from ordering entirely, and lets any challenge gate a zone (clear the previous boss, reach a level, defeat N of something, …) — so non-linear or milestone-based progression needs no code change. The canonical setup points a zone's `UnlockChallengeId` at the previous zone's **"clear" challenge** — the one that already grants that boss's loot — so "clear a zone → its challenge completes → the next zone unlocks" falls out for free. `Order` remains purely for nav direction and display.
- **Locking is opt-in, authored content.** `UnlockChallengeId` defaults to `null`, so the mechanism ships inert: no zone is locked until a gate is authored through the admin tools (consistent with the intrinsic-vs-authored-content split — static content lives in the admin tools, not migrations). Which challenge gates which zone is therefore a content/design decision, not hardcoded logic.
- **Enforced on both ends.** The frontend gates the zone-nav arrows (a locked neighbour shows a distinct lock affordance and is not navigable) and the Zone-Cleared overlay's "Next zone unlocked" line is shown only when a clear actually flips the next zone open. The backend independently enforces the same rule as anti-cheat: a zone change (the `NewEnemy` socket command, via `BattleService.StartBattle`) into a locked zone is ignored (the player keeps fighting their current zone) and a `ChallengeBoss` against a locked zone is refused — so a tampered client cannot enter a zone it has not unlocked.
