# Spike #1393 — Unlock conditions for each page

## The question, as filed

"Add unlock conditions for each page. e.g. don't show the proficiencies page until you've unlocked
a proficiency. (This will also provide an opportunity to stagger feature tutorials.)"

## Current state (what already exists)

- **The screen registry already has one unlock axis.** `GAME_SCREENS` (`UI/src/routes/game/screens/screen-defs.ts`)
  is a flat list of `ScreenDef`s; a screen with `requiresRole` (today, only `admin`) is filtered out
  for users without that role by `visibleScreens`, called once in `routes/game/+page.svelte`
  (`const screens = $derived(visibleScreens(GAME_SCREENS, roles))`). Extending this to a second,
  progress-based axis is a natural fit for the existing shape — a predicate alongside `requiresRole`,
  filtered the same way.
- **`docs/spikes/1392-ui-tutorials.md` already anticipated this issue** as the natural upgrade for its
  screen-anchored tutorial trigger: "Once #1393 lands per-screen unlock predicates, this trigger should
  reuse them (fire when a screen transitions unlocked *and* active)." That work shipped without waiting
  on this issue (screen-anchored lessons currently trigger on "first time ever active," which is
  well-defined standalone) — so #1392 is not blocked on this, but a predicate this issue adds would be
  a drop-in improvement there.
- **The issue's own example already has a different, shipped answer.** The Proficiencies screen
  (`routes/game/screens/proficiencies/Proficiencies.svelte`) has a dedicated empty state for exactly
  the zero-proficiencies case: *"Acquire a skill that trains a proficiency and its path will appear
  here, ready to decipher."* Synthesis has the same pattern verbatim in spirit: *"No formulae discovered
  yet... Acquire a skill that feeds a recipe and it will appear here."* These aren't placeholders —
  they're onboarding copy that explains *how* to unlock the thing, which a hidden nav entry can't
  deliver. Hiding the screen would remove that guidance, not just avoid an empty page.
- **Player-side proficiency data is available** (`$stores/proficiencies.svelte.ts` → `playerProficiencies`),
  so the issue's example condition is technically implementable — it's just that the screen already
  handles it, arguably better than hiding would.
- **Card Game is not a stub.** `routes/game/screens/card-game/` ("The Initiative Loom") is a fully
  playable standalone minigame, not placeholder content — #259 is about integrating its concepts with
  the Skills system later, not about the screen being unfinished today. It doesn't fit "hide until
  ready" either.

## Audit: does any current screen actually warrant hiding?

Went through all fifteen registered screens (`GAME_SCREENS`) individually:

| Screen | Always-relevant from character creation? | Notes |
| --- | --- | --- |
| Fight, Inventory, Attributes, Options, Switch, Quit | Yes | Core loop / chrome. |
| Skills | Yes | Every new character starts with a kit skill; never truly empty. |
| Card Game | Yes | Standalone playable feature, not gated content. |
| Challenges | Yes | Shows live progress from the first challenge onward. |
| Synthesis, Proficiencies | Effectively yes | Empty at level 1, but already has dedicated onboarding-copy empty states (see above) — hiding would regress that UX, not improve it. |
| Attribute Breakdown, Stats, Codex | Yes | Detail/reference views; meaningful (if sparse) from turn one, and sparse-but-present is normal for a reference/compendium screen. |
| Help | Yes | Reserved-then-built tutorial reading room (#1392); harmless with zero lessons unlocked. |
| Admin | Already gated | Existing `requiresRole` precedent — not this issue's concern. |

**Finding: no screen in the current registry has a clean case for hard-hiding.** Every non-core screen
that starts "empty" already has (or is expected to have, per its own screen doc) a deliberate empty
state that teaches the player how to fill it — which is the better onboarding tool for a screen that's
reachable but has nothing in it yet, versus removing the nav entry and the guidance with it.

## Recommendation

**Don't build a per-screen unlock-predicate mechanism speculatively.** `requiresRole` earned its place
in `ScreenDef` because a real, current need (hiding Admin from non-admins) used it immediately; adding
an `isUnlocked` axis today would be plumbing with zero call sites, which the project's own guidance
(CLAUDE.md: avoid designing for hypothetical future requirements) argues against. The mechanism is
cheap to add **when a screen actually needs it** — same shape as `requiresRole`, a predicate over
player state instead of roles, filtered in `visibleScreens` the same way — so there's no lock-in cost
to deferring it.

**When it will earn its place:** a genuinely-not-ready-yet screen gets added to the registry (e.g. a
future Class or Card-Game-progression screen that's meaningless before some milestone), or the
mechanic-anchored tutorial trigger (#1392 area 1) wants "screen transitions unlocked and active" for
real instead of "first time ever active." Either is a small, concrete follow-up at that point.

**Softer alternative already worth doing, if the maintainer wants **something** out of this issue:**
audit the reference/detail screens (Codex, Stats, Attribute Breakdown) for whether they have the same
quality of "nothing here yet" empty-state copy that Proficiencies/Synthesis do, and backfill any that
don't — that directly serves the issue's underlying goal (a new player isn't confused by an empty
page) without hiding navigation.

## Open question for the maintainer

Does this reasoning hold, or is there a concrete screen (current or planned) you already have in mind
that should be hidden pre-unlock? If so, naming it turns this into a small, well-scoped follow-up
(add the predicate axis + gate that one screen) rather than the speculative version this spike argues
against. Absent that, recommend closing this issue as "researched — no current candidate; the
`requiresRole` pattern is the template to extend if/when one appears."
