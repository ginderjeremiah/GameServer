# Battle Mechanics

Battling is the core mechanic of the game, and it is designed to be a continuous and engaging experience for players. Players will idle in different zones, automatically fighting monsters that spawn in those zones. During battle players and enemies will automatically use their skills, until one side is defeated or a timeout is reached (or the player changes zones). Players will earn experience points and progress towards Challenges as they battle.

It is important to note that the game is designed to replicate battles on both the frontend and backend as an anti-cheat measure. The backend is the source of truth for all game logic and state, but the frontend also simulates battles in real-time to provide a responsive and engaging user experience. This means that all battle logic must be implemented in both the frontend and backend, the results must be consistent between the two, and the backend must be able to simulate the battle very quickly (performance matters significantly there).

There are some choices already made in the design of the game to support this:

- All battle RNG MUST use the same implementation (currently Mulberry32) that can be seeded with the same value on both the frontend and backend to ensure consistent results.
- The "state" of a player is snapshot when combat starts. This means that all relevant player stats, items, and skills are included in the combat state, and no external factors will influence the result of the battle once it has started. Since the backend does not simulate the battle until AFTER it is resolved in the frontend, this ensures that the battle will be consistent regardless of any changes that may occur to the player's state during combat.
- The battle simulation "runs" in 40ms ticks, meaning that all actions are processed in discrete intervals of 40ms. This allows for a consistent simulation and is a reasonable approach for a performant simulation on the backend while still providing a responsive experience on the frontend. The frontend should contain additional logic to interpolate values between ticks for smoother animations, but the core battle logic will be processed in these discrete ticks.

# Character Progression and Attributes

Character progression in the game is primarily driven by leveling up and allocating stat points. As players defeat monsters and complete challenges, they earn experience points (XP) that contribute to their character's level. When a player levels up, they receive stat points that can be allocated to core Attributes such as Strength, Agility, Intellect, etc.

Attributes are somewhat of a work-in-progress feature, but the general idea is that they will provide various bonuses and effects that influence combat, other Attributes, and potentially other mechanics in the game. Currently they come from stat allocations and equipment, but this will be expanded in the future. For example, a Skill may receive a damage bonus based on the player's Strength Attribute, and the player's MaxHealth Attribute will receive a bonus based on their Strength as well. Attributes are designed to go above and beyond the traditional RPG stats and provide a system for complex effects.

The attribute system is the substrate for timed skill effects (see [Skills](#skills)): applying an effect adds a real attribute modifier to the target mid-battle and expiry removes it, so derived attributes cascade naturally (a temporary Strength buff also raises MaxHealth, exactly like a permanent one). Damage-over-time and heal-over-time are realized the same way, through two **per-second** attributes — `DamageTakenPerSecond` and `HealthRegenPerSecond` — that an end-of-tick simulator phase consumes (a poison is then pure data: a debuff that adds `DamageTakenPerSecond` to the opponent for a duration). See [Skill Effects](#skill-effects) for the DoT/HoT runtime rules.

## Attribute value conventions

Percentage- and multiplier-style attributes are stored so they are consumed **without transformation** in the battle math:

- **Percentage attributes** are stored as decimal fractions (`0.05` = 5%) and used as-is — a chance is compared directly against a `[0,1)` RNG draw — and rendered scaled ×100 (`0.05` → `5%`).
- **Multiplier attributes** carry a base ≥ 1 and are read directly as the multiplier. `CooldownRecovery` is a base-`1` cooldown multiplier: a static `1.0` base plus `0.004·Agility + 0.001·Dexterity` (a typical AGI 20 / DEX 10 build sits at `1.09` ≈ +9% charge speed). Reading the attribute directly means a multiplicative modifier scales intuitively — a `×2` `CooldownRecovery` buff genuinely doubles charge speed rather than nudging the old `1 + CDR/100` value. Like the percentage attributes, it renders scaled ×100 (`1.09` → `109%`).

This convention is the foundation the player crit/dodge/block attributes build on — see [Critical hits, dodge & block](#critical-hits-dodge--block) below and the [spike](./spikes/178-player-crit-dodge-block.md).

## Critical hits, dodge & block

Players can land **critical hits** (deal increased damage), **dodge** (fully avoid an incoming attack), and **block** (flatly reduce one). Enemies do **not** in this version — the asymmetry keeps the shared battle RNG simple and is a deliberate seam for later enemy parity. The mechanic runs in the seeded battle simulation (so the frontend and backend agree tick-for-tick; see [backend-battle.md](./backend-battle.md#battle-runtime--seeded-crit--dodge--block-player-only)):

- **Crit** multiplies the skill's raw damage by `CriticalDamage` (a base-≥1 multiplier read directly) **before** Defense is subtracted, so it can punch through Defense.
- **Dodge** zeroes the incoming hit entirely.
- **Block** subtracts a flat `BlockReduction` alongside Defense (`max(raw − Defense − BlockReduction, 0)`).

`CriticalChance`/`DodgeChance`/`BlockChance` are decimal probabilities (`0.05` = 5%) compared directly against the RNG draw. They are **sourced from the core attributes** so the mechanic is live from raw allocations — `Dexterity`/`Luck` feed crit, `Agility` feeds dodge, `Endurance` feeds block — and `CriticalDamage` (base `1.5`) and `BlockReduction` (base `2`) carry a base so a crit/block matters before any gear. Items, item-mods, and skill-effects can also grant any of them through the normal modifier path. The derivation coefficients are a conservative strawman expected to be tuned during balancing (the exact formulas live in `StaticAttributeModifiers`). Crit/dodge/block outcomes surface to the player in the combat log, and the attributes themselves appear on the attribute-breakdown screen, rendered with the percentage convention above (`CriticalDamage`'s base-`1.5` multiplier reads as `150%`).

## Experience Rewards

The XP a victory awards scales with how well the enemy is matched to the player's power, so fighting something around (or above) your level pays off and grinding trivial enemies does not. The reward (`DefeatRewards`, computed server-side and sent to the client) is the enemy's attribute power times a difficulty multiplier derived from the ratio of the enemy's power to the player's: within a ±20% band the multiplier is `1`, and outside it the reward scales quadratically with the ratio (over-level enemies pay out more, under-level enemies far less).

**The quadratic multiplier is capped (`ServerGameConstants.MaxExpRewardMultiplier`, currently `4`).** Without a ceiling, `ratio²` grows without bound, so an enemy far above the player (e.g. a challenge boss cleared underleveled) mints a disproportionate single-battle payout — an exploitable balance cliff. The cap saturates at a power ratio of 2× (an enemy twice the player's power already pays the maximum bonus); ratios beyond that plateau rather than scaling quadratically. So the intended shape is: `1` inside the ±20% band, quadratic `ratio²` outside it, flattening to `4×` at and above a 2× ratio. The cap bounds the *multiplier* but not the enemy's raw power, so the final reward is additionally clamped into `int` range before it is returned — an extreme authored enemy power is clamped rather than overflowing the cast to a wrapped (negative) value. Because that bounded reward is the only legitimate input to `Player.GrantExp`, the level-up loop it drives is bounded too (`GrantExp` additionally clamps any grant to `ServerGameConstants.MaxExpPerGrant` as a backstop against a tampered/replayed value).

"Power" on both sides is measured the same way — **the sum of the additive *core* attribute amounts** (Strength, Endurance, Intellect, Agility, Dexterity, Luck). Comparing like with like is the key invariant: derived attributes (e.g. MaxHealth) are excluded because they are computed *from* the core attributes (counting both double-counts the same investment, and a player's derived attributes are not part of their raw modifier set), and multiplicative modifiers are excluded because their amount is a scaling factor rather than a flat point total. Notably the player's measure is their **allocated** attribute total (allocations plus equipment), **not** the count of stat points they have been granted — a new player's starter allocation counts even though it was never "gained" from a level-up. The player's power is read from the **battle snapshot** frozen at battle start (the same state the battle is simulated against), not the live player aggregate, so the reward stays consistent with the fought battle and cannot be inflated by reallocating stats or unequipping gear between battle start and the victory claim.

# Skills

Skills are a fairly underdeveloped feature in the game, but the general idea is that they will provide active abilities for players that are automatically used during battles. Beyond dealing damage, a skill can carry **timed effects** — buffs/debuffs that raise or lower an attribute on the caster or the opponent for a duration (see [Skill Effects](#skill-effects)).

Skills are **unlockable through Challenges**, mirroring the item/mod reward path: a challenge may carry an optional `RewardSkillId`, and completing it unlocks that skill via `Player.UnlockSkill`. An earned skill is added to the player's unlocked set **unselected** — unlocking a skill does not auto-equip it. New players still start with their fixed starter skills equipped.

Players choose which unlocked skills are equipped (and in what order) through the skill **loadout**. The equipped set is **capped at 4** (`GameConstants.MaxSelectedSkills`, matching the enemy loadout cap) and its **order is meaningful** — it drives in-game display order and is the current (incidental) tie-break when two skills come off cooldown on the same tick. The loadout is replaced **atomically**: a single `SetSelectedSkills(orderedSkillIds)` command handles select, deselect, and reorder through one path, with the cap and unlocked-only rules enforced on the backend as **anti-cheat**. Players manage the loadout through the in-game Skills screen (see the Skills screen section in [frontend-screens.md](./frontend-screens.md#skills-screen-the-loadout-inspector)).

In the future, Skills will be expanded to include a wider variety of effects, such as healing and utility effects, and skills may become unlockable through means beyond Challenges.

## Skill Effects

A skill may carry any number of authored **effects**. Each effect is a timed attribute modifier with a `Target` (the caster — `Self` — or the `Opponent`), an `Attribute`, a modifier type (additive/multiplicative), an `Amount`, and a `DurationMs`. When the skill fires, each effect is applied to its target as a real attribute modifier that expires after its duration. The runtime rules (which the frontend and backend implement identically, since battle logic runs on both sides) are:

- **Damage first, then effects.** A skill computes and deals its damage from the pre-effect attribute state, then applies its effects — so a self damage-buff never boosts the very hit that carries it. Because skills resolve in loadout order, an earlier slot's effect *does* influence a later slot firing on the same tick.
- **Timed, in 40ms ticks.** A duration of `DurationMs` influences `DurationMs / 40` ticks (counting the tick it was applied on); effects expire at the start of a tick, before any skill fires.
- **Refresh, not stack.** Re-applying an already-active effect refreshes its remaining duration to full rather than adding a second modifier — magnitudes never stack from the same authored effect (distinct effects on the same attribute do stack).
- **MaxHealth never heals on a buff.** When an effect raises MaxHealth, current health is left unchanged (no free healing); when MaxHealth drops below current health, current health is clamped down to the new maximum.

Damage-over-time and heal-over-time are just effects on two **per-second** attributes — `DamageTakenPerSecond` and `HealthRegenPerSecond` — applied by an **end-of-tick phase** that runs after both battlers' skills resolve (only while both still live). A poison is then pure data: a debuff that adds `DamageTakenPerSecond` to the opponent for a duration; a regen, a self buff to `HealthRegenPerSecond`. Its rules:

- **Per-second units.** The phase applies `value × 40 / 1000` per 40ms tick.
- **Enemy resolves first.** The enemy's DoT/HoT is applied before the player's, so an enemy DoT kill awards victory before the player's DoT lands — a same-tick mutual DoT kill favours the player (mirroring the skill-exchange order).
- **Damage before heal, death between.** For each battler `DamageTakenPerSecond` applies first (**bypassing Defense**) and death is checked, then `HealthRegenPerSecond` heals (capped at MaxHealth) — so a lethal DoT tick kills even when the same tick's heal would have saved the battler.
- **Statistics.** DoT dealt to the enemy counts toward `DamageDealt` (but not the highest-single-attack or per-skill totals — per-skill DoT attribution is deferred); DoT taken by the player counts toward `DamageTaken`; the player's post-cap healing feeds the `DamageHealed` statistic.

Effects are deterministic in this version (they always apply when the skill fires — no proc chance), and durations are time-based. Authoring effects on skills is done through the admin Workbench's skill editor.

# Items, Item Mods, and Tags

In early versions of this game, items dropped randomly from enemies with random modifications based on matching tags. This tag system has data like: "Weapon", "Sharp", "Two-Handed", etc. An item would have a set of tags, and item modifications would also have tags. When an item dropped, the game would roll for modifications that had matching tags to the item.

This system was ultimately scrapped in favor of a more deterministic system where items and item modifications are unlocked through Challenges. This change was made to provide a more engaging and rewarding progression system for players, as well as to allow for more strategic decision-making when it comes to character builds and playstyles. The tag system still exists as a means of determining which item modifications can be applied to which items. For example, a "Sharpened" modification may only be applicable to items with the "Sharp" tag.

# Content Lifecycle — Retiring Reference Data

Authored reference content (items, item mods, skills, enemies, zones, challenges) is **retired**, never hard-deleted. Retiring takes a piece of content *out of circulation* — the game stops introducing it anew — while keeping it permanently valid for anything that already references it. This is a deliberate design choice for an idle game where content ids are baked into persisted player state (equipped items, applied mods, unlocked skills, completed challenges, battle snapshots): a player who already earned a now-retired item keeps it and it behaves exactly as before, but new players won't encounter it.

- **Retired ≠ deleted.** A retired record stays in the catalogue and is fully resolvable; it just isn't offered for new acquisition or encounters. Retiring is reversible (reinstate).
- **What "out of circulation" means today:** a retired enemy no longer spawns in random idle encounters. Because acquisition is otherwise authored — items/mods/skills come from specific challenge rewards, bosses and zone unlocks are authored by id — retirement doesn't silently pull content a player already has or a challenge already grants; it only stops *new* organic exposure (random spawns). Admin authoring pickers likewise exclude retired records from new references, while keeping an existing authored reference visible (#295).
- **Why not delete:** deleting a non-terminal record would corrupt the id-as-index lookups the whole game relies on and orphan persisted references. See [backend.md → Retiring reference data](./backend.md#retiring-reference-data-content-lifecycle) for the mechanism and the deferred per-entity edge cases.

# Challenges and Statistics

Challenges and Statistics are relatively new additions to the game, and their design is still evolving. Challenges are a way for players to unlock new content and progression by completing specific tasks or reaching certain milestones in the game. They can range from simple objectives, like defeating a certain number of monsters, to more complex ones, like completing a zone without taking any damage.

Not all Challenges are tied to Statistics and vice versa, but many will be. Statistics are tracked metrics that can be used to gate content and progression in the game. For example, a Challenge may require a player to have a certain number of kills with a specific weapon type, which would be tracked as a Statistic.

## Battle Outcomes (`BattlesWon` / `BattlesLost` / `BattlesAbandoned`)

Every completed battle is classified by its **outcome**, and the three outcomes are mutually exclusive so they partition all battles:

- **`BattlesWon`** — the enemy died (the player won).
- **`BattlesLost`** — the player died.
- **`BattlesAbandoned`** — neither combatant died; the player walked away from an unfinished fight (e.g. retreating from a boss or switching zones mid-battle). Abandoning is deliberately **not** counted as a loss, since the player never actually fell (#202).

The classification keys off the simulated outcome (`victory` / `playerDied`), not which code path ended the battle — a battle abandoned via `AbandonBattle` in which the player had in fact already died is still a loss. All three are recorded both globally and per-enemy. Like `BattlesLost`, `BattlesAbandoned` is a "lower is better" statistic on the Statistics screen.

## Challenge Goal Comparison Direction

Most challenges are _accumulating_ goals: the tracked statistic increases over time and the challenge is completed once it reaches **at least** the goal (e.g. "defeat 100 enemies"). However, some statistics are minimized rather than maximized — for example, `FastestVictory` records the lowest victory time, where lower is better. A `TimeTrial` challenge ("win a battle within N seconds") is therefore satisfied when the tracked value is **at or below** the goal, which is the opposite comparison. For **at or below** goals, a tracked value of `0` is treated as "no data yet" (e.g. the player has not won a qualifying battle) and does **not** complete the challenge — otherwise a brand-new player with no victories would instantly satisfy every time trial. This matches how `FastestVictory` is stored, where `0` is the sentinel for "no victory recorded".

## Zone Clears (`ZonesCleared`)

A zone is considered **cleared** when the player wins a battle started through the dedicated **"Challenge Boss"** action against that zone's single dedicated boss. The clear keys off an explicit dedicated-boss marker (`PlayerState.IsBossBattle` + the challenged `BattleZoneId`, threaded through to the battle-completed resolve), **not** the enemy's `IsBoss` flag or the player's `CurrentZoneId` — so only the challenge path clears a zone, never a boss that happens to roll out of a random spawn table. (See the boss battle-setup decision in [backend-battle.md](./backend-battle.md#battle-setup-enemy-encounter) for the dedicated-boss model itself.)

A dedicated-boss victory updates two distinct statistics, which are deliberately kept separate so each answers a different question:

- **`ZonesCleared` counts _distinct zones ever cleared_, not boss-victory events.** The per-zone entry (keyed by zone id) is a binary **"cleared" flag** that flips from `0` to `1` on the zone's first clear and stays there; the global counter increments **only** on that `0 → 1` transition. Re-farming a zone's boss therefore never re-counts the zone — clearing two different zones once each gives a global `ZonesCleared` of 2, while clearing one zone's boss fifty times leaves it at 1. A `ZonesCleared` challenge can still target either "clear any zone" (global) or a specific zone (per-zone flag).
- **`BossesDefeated` is the _farm counter_.** It increments on **every** dedicated-boss victory, tracked both globally and **per-boss** (keyed by the boss enemy's id), so a challenge can target either "defeat any boss N times" or "defeat this specific boss N times". Because per-boss rows are written, `BossesDefeated`'s declared `StatisticType.EntityType` is `Enemy` (matching the rows actually recorded — see the [backend.md](./backend.md#http-vs-websocket-communication) note that `GetEntityType` must agree with what `RecordBattleCompleted` writes).

This split (refined in #188 from the #89 stop-gap that tallied every boss victory as a global "clear") keeps "how many zones have I beaten" and "how many times have I farmed this boss" as separate, individually meaningful numbers. The dedicated-boss **model and Challenge Boss action** themselves landed via the #96 spike (#186 data model, #187 battle action).

## Zone Progression — locking zones behind challenges

Zone navigation is gated so later zones become real progression rather than being freely reachable from the start (#190). A zone carries an optional **`Zone.UnlockChallengeId`**: it is navigable iff that field is `null` (always open) **or** the player has completed the referenced challenge.

- **Gated on a challenge, not on `Zone.Order`.** A naive "zone `N` needs zone `N-1` cleared" chain bakes a rigid, contiguous linear order into the code and is awkward to evolve. Gating on a _challenge_ instead reuses the existing challenge/unlock system (the same machinery that unlocks items and mods), decouples gating from ordering entirely, and lets any challenge gate a zone (clear the previous boss, reach a level, defeat N of something, …) — so non-linear or milestone-based progression needs no code change. The canonical setup points a zone's `UnlockChallengeId` at the previous zone's **"clear" challenge** — the one that already grants that boss's loot — so "clear a zone → its challenge completes → the next zone unlocks" falls out for free. `Order` remains purely for nav direction and display.
- **Locking is opt-in, authored content.** `UnlockChallengeId` defaults to `null`, so the mechanism ships inert: no zone is locked until a gate is authored through the admin tools (consistent with the intrinsic-vs-authored-content split — static content lives in the admin tools, not migrations). Which challenge gates which zone is therefore a content/design decision, not hardcoded logic.
- **Enforced on both ends.** The frontend gates the zone-nav arrows (a locked neighbour shows a distinct lock affordance and is not navigable) and the Zone-Cleared overlay's "Next zone unlocked" line is shown only when a clear actually flips the next zone open. The backend independently enforces the same rule as anti-cheat: a zone change (`NewEnemy`/`StartBattle`) into a locked zone is ignored (the player keeps fighting their current zone) and a `ChallengeBoss` against a locked zone is refused — so a tampered client cannot enter a zone it has not unlocked.

# Logs

Logs here refer to messages displayed in the UI of the game for various events, such as combat actions, item drops, level-ups, and other significant occurrences. Logs are an important part of the user experience, as they provide feedback and information to players about what is happening in the game. Logs can be filtered by type (e.g., damage dealt, experience gained, level-ups) and are meant to be informative enough that the player can step away from the game and see if anything significant happened while they were away or to have a record of what happened during a battle. Logs should only ever be generated on the frontend, as they are purely a UI element and do not affect any game logic or state. The backend should not be concerned with these kinds of logs, as its primary responsibility is to maintain the integrity of the game state and logic.

Skill effects have their own log type (`SkillEffect`), distinct from raw `Damage`, so the player can toggle them independently. It covers two kinds of message, both emitted only from the live battle loop: a one-line announcement when a timed buff/debuff is *newly* applied (a refresh is not re-logged — the active-effect chip's countdown already conveys it), and a per-second summary of damage-over-time / heal-over-time. Because DoT/HoT applies every 40ms tick (25 times a second), it is **never logged per tick** — each over-time channel is accumulated and emitted as a single line per second (with any partial second flushed at battle end), keeping the feed readable.
