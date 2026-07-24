# Spike #259 — Card minigame (The Initiative Loom): staging plan

## The question, as filed

"Research and plan implementation of card minigame." An initial design demo already exists
(`UI/src/routes/game/screens/card-game`), and the issue's own note was that porting real game
concepts into it "will likely have to wait until Skills are more fleshed out as a concept, since
they directly relate to the cards that will exist in the card game."

That blocker is worth re-checking before planning: Skills have grown substantially since this
issue was filed (June) — damage-type splits, timed effects (buffs/DoT/HoT), the weapon-match gate,
acquisition channels, cooldown cadence via `CooldownBonus`/`CooldownBonusMultiplier` — all
documented in `game-design.md` § Skills. So the stated precondition is met. What replaces it as the
actual blocker, this spike's finding, is a **simulation-model mismatch** (below), not an
immaturity of Skills.

## Current state (what already exists)

- **`game-design.md` already commits to a design intent** (§ "The Initiative Loom"): a real-time
  card-boss duel, the game's one deliberate active-play break from the idle core loop. Cards are
  multi-tick spans on a scrolling timeline that resolve when they cross the moving **NOW** line —
  tempo management, not build power. The tempo attributes every build has drive the feel: Agility
  → draw speed, Dexterity → hand cap, Luck → crit cadence; offense scales off `max(STR, INT)` so
  pure-STR and pure-INT builds hit equally hard. Status is explicitly "frontend-only demo with
  fixed card damage — backend authority, admin-authored content, and the real attribute wiring are
  staged later (tracked by #259)."
- **The demo is a complete, well-factored client-only engine**, not a rough sketch:
  - `UI/src/lib/card-game/loom-core.ts` — pure, framework-free mechanics (attribute→stat formulas,
    half-open span coverage, no-overlap lane placement, strike resolution). Deterministic given an
    injected `rng`.
  - `UI/src/lib/card-game/loom-game.ts` — the stateful engine (`LoomGame`), driven by
    `advance(dt)` from a `requestAnimationFrame` loop with **variable, wall-clock `dt`**. Enemy
    cadence, crit marks, and the boss ("The Warden") are hardcoded schedules with light RNG jitter.
  - `UI/src/lib/card-game/cards.ts` — four hardcoded card defs (`guard`, `dodge`, `slash`,
    `channel`) with a fixed starter deck. The file's own header comment already anticipates the
    real-data mapping: *"Strike = a basic attack coloured by max(STR,INT), Channel = a rare skill
    card, etc."*
  - The screen (`CardGame.svelte` + `card-game-view.svelte.ts`) is a polished drag/click/hotkey
    interaction layer (pointer events, not mouse-only, for touch/pen support) documented in
    `frontend-screens.md` § "Card Game screen (the Initiative Loom)".
- **No reward/economy hook exists today.** The screen is reachable from the nav with no zone gate,
  no challenge tie-in, and (per the outcome text, "Loot drops") only a placeholder win message — it
  grants nothing real. `content-design.md` does not mention it at all.
- **No enemy content model exists for it.** "The Warden" is a single hardcoded schedule of enemy
  hit/block/channel timers with jitter — there is no per-`Enemy` authored loom encounter, and
  nothing binds a duel to a specific zone/boss the way `ChallengeBoss` binds to `Zone.BossEnemyId`.

## The real blocker: two incompatible simulation models

The core battle loop (`game-design.md` § Battle Mechanics) is built on a set of hard invariants:
fixed **40ms discrete ticks**, a single shared seeded RNG (Mulberry32) so frontend and backend
agree tick-for-tick, and a **snapshot-at-combat-start** rule so nothing external can influence an
in-flight result — because the backend re-simulates every battle after the fact as anti-cheat and
the two simulations must match exactly.

The Loom is built the opposite way: `advance(dt)` runs off wall-clock time in a `requestAnimationFrame`
loop with a variable `dt` (clamped, not quantized), player input (drag-to-aim, press-and-hold
Reflex) directly perturbs the simulation moment-to-moment, and nothing about it is seeded or
tick-quantized. This isn't a shortcut that was skipped in the demo — it's the *right* engine for
what the Loom is trying to be (a twitch/tempo minigame), and retrofitting the main battle system's
40ms-tick-and-replay model onto it would fight the genre it's aiming for (aiming a card by hand
needs sub-tick timing resolution and immediate feedback, not a 40ms-quantized authoritative
replay).

So "give it backend authority" cannot mean "make it battle-parity like the idle loop." It means
picking a **different, weaker trust model appropriate to what's actually at stake** — which in turn
depends on the reward question below.

## Directions considered

**How much does a duel outcome matter economically?**
- *Full parity* (mirror the idle-battle model: seed the duel, replay deterministically server-side): rejected above — the two simulations serve genuinely different feels, and forcing tick-quantization onto real-time drag-aim gameplay would make the "active, twitchy" duel feel like the idle loop with extra steps, defeating its stated purpose.
- *Trust-with-bounds* (client reports the outcome and a summary trace; server sanity-checks bounds — max possible damage per resolved card, elapsed-time-vs-HP-delta plausibility — the same shape `AbandonBattle`/`BattleService` already use to bound a client-reported value rather than trust it blindly): workable, but only worth building once the Loom actually grants something worth cheating for.
- *No economy in v1* (**recommended**): ship the real-data wiring (attributes, real skill-derived cards, a real boss) with **no exp/loot grant on victory** — a skill-expression toy, not a reward faucet. This sidesteps the anti-cheat question entirely for the first real milestone, matches "loot drops" never having been implemented anyway (currently just flavour text), and defers the trust-model decision to whenever a reward is actually proposed, when it can be sized to match whatever bound is cheap to build.

**How do real Skills become cards?**
- *Literal 1:1 port* (every equipped skill becomes exactly one card, mechanically): doesn't work — the Loom's `block`/`dodge` card kind has **no real-battle counterpart**. Dodge/Parry in the main game are RNG-opt-in enablers the player never actively triggers; there is no player-driven "block" action in the core battle model at all. A literal port would need to invent a block mechanic in the core game just to justify a card, which is backwards.
- *Reskin only* (keep the four fixed archetypes, just recolor `slash`'s number/label from the player's actual basic attack skill): cheap, but makes "wired to real data" nearly meaningless — the deck never reflects what skills the player actually has equipped, so investing in Skills never changes the minigame.
- *Archetype-classified mapping* (**recommended**): keep the Loom's own small vocabulary of card *kinds* (it's a distinct minigame with its own ruleset, per `game-design.md`'s framing — "tempo management rather than build power" — not a re-simulation of core combat), but **derive each equipped skill's card** from its authored shape: an instant-ish, no-effect damage skill → `attack` card (windup from its cooldown, `dmg` from its damage at the player's snapshotted attributes); a skill carrying a self-buff/defensive effect → a new `guard`-like card kind; a skill with a long cooldown and a bigger hit → `channel`. This needs an explicit, authored (or at least deterministic-from-data) classification rule per skill shape — a content/design decision, not a mechanical port, and probably wants a small new "how does this skill behave in the Loom" field or convention rather than inferring it purely from heuristics on `SkillEffect`/damage numbers.
- Whichever is chosen, the **deck composition** question (how many copies of each equipped skill's card, and does upgrading gear/proficiency change the deck) is itself open — the current demo's fixed 14-card `STARTER_DECK` composition is a strawman with no real-data equivalent yet.

**Where does the boss come from?**
- *New standalone content type* ("Loom Encounter": an authored schedule of hit/block/channel timings, hand-tuned per fight): matches what exists today, but duplicates the `Enemy` authoring surface and needs its own admin Workbench UI.
- *Derive from existing `Enemy` reference data* (its equipped skill pool becomes its own card-like schedule, same classification rule as the player side): more consistent with "wired to real data," and means an authored Enemy needs no Loom-specific fields — but the schedule-generation logic (when does the enemy "cast" which skill) needs its own tempo model, since enemies today act on cooldown-charge ticks in the core sim, not hand-authored timer offsets.
- No recommendation yet — this is a genuine fork between "the Loom has its own lightweight enemy-authoring surface" and "the Loom reads the same `Enemy` data everything else does." Worth a maintainer opinion since it affects how much new admin tooling this needs.

**How is the Loom reached in the idle flow?**
- Left fully open. Candidates: an always-available standalone screen (as today, i.e. no gating); a per-zone-boss alternative to `ChallengeBoss` (duel the boss instead of idle-fighting it); a new challenge category. Deferred — doesn't need resolving before the reward-free v1 above, and is entangled with #1393's now-answered "no current screen needs unlock gating" finding (a duel screen with no rewards has the same profile as Proficiencies/Synthesis: always reachable, onboarded rather than hidden).

## Recommended phased direction

1. **v1 (real data, no economy):** wire the demo's `agi`/`dex`/`luck` sandbox sliders to the
   live player's real attributes (snapshotted like a battle, refresh on the same triggers the
   attributes screen already uses); replace the fixed 4-card deck with one derived from the
   player's actually-equipped skills via an explicit classification rule (needs the design call
   above); replace "The Warden"'s hardcoded schedule with a specific authored `Enemy` (even if v1
   keeps its schedule hand-tuned rather than derived — that half can follow). No exp/loot grant;
   the win/loss screen stays flavour-only. This alone resolves "wait until Skills are more fleshed
   out" and is independently shippable/testable without touching anti-cheat.
2. **v2 (integration + economy):** once v1 is live and the deck/classification rule has been played
   with, revisit reward wiring with a concrete, small reward (not full exp parity) and the
   trust-with-bounds model above, plus decide the idle-flow entry point.
3. **v3 (content depth):** derive enemy schedules from real `Enemy` skill pools instead of
   hand-authored timers, if the Enemy-data direction above is chosen.

This spike stops at v1's design questions rather than filing implementation issues — per this
repo's spike convention, an ideation issue like this should get a collaborator pass on the open
forks above (skill→card classification, and the encounter-content fork) before scope is cut into
concrete sub-issues, the same process #1392 followed.

## Open questions for the maintainer

1. Does the "no economy in v1" scoping sound right, or is a reward (even a small flavour one) a
   hard requirement for the first real milestone?
2. Skill→card classification: is deriving card kind from a skill's authored shape (damage +
   effects + cooldown) the right approach, or would you rather author it explicitly per skill (a
   new field), given "block" has no core-battle analogue to derive from?
3. Enemy content: a Loom-specific authored schedule, or derive from existing `Enemy` skill pools?
4. Any opinion yet on where the Loom sits in the idle flow (standalone screen vs. zone/boss-bound),
   or is that genuinely a v2 question?
