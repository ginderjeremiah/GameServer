# Spike #1392 — UI tutorials (teaching-lane blurbs)

> **Revision (2026-07-04):** the first draft of this doc (PR #1585) recommended toast-delivered, hardcoded, `localStorage`-dismissed blurbs and was merged — and its first implementation issue picked up — without collaborator review, which the repo's spike conventions require before planning begins. On review, the delivery, authoring, and persistence calls were revised; the trigger model survived intact. The superseded direction is documented under "Directions considered" rather than erased.

## The question, as filed

"Plan out UI tutorials for each page and develop a robust system for displaying tutorials," with four questions folded in from `docs/content-design.md` §2 (which treats tutorial blurbs as one of two teaching lanes, alongside zone design, for mechanics a player can grasp from one good explanation rather than sustained pressure):

- **Trigger model** — what fires a blurb?
- **Authoring** — reference data (riding #1390's export/seed + Workbench) or hardcoded?
- **Per-page vs. per-mechanic** — the issue frames "tutorials for each page," but content-design.md's named candidates (crit/dodge variance, cooldown charging, idle-loop basics) are mechanic-level, not page-level.
- **Dismissal/replay** — one-shot or revisitable, and how does dismissal state persist?

content-design.md §2 calls this "a dependency of the content arc, not just UX polish" — the planned zone arc assumes blurbs exist to carry lessons that don't warrant a whole zone.

## Current state (what already exists)

- **Screens aren't routes.** `UI/src/routes/game/screens/` is a config-driven registry (`screen-defs.ts`); the active screen is local state in `routes/game/+page.svelte`, not a URL. "First visit to a page" must hook the screen-activation point, not `afterNavigate`. A **reserved-but-unbuilt `help` screen** (`key: 'help', built: false`, settings group) is an already-registered slot with no component.
- **The content-events derivation layer is merged** (#1586, `UI/src/lib/common/content-events.ts`): pure before/after-diff primitives (`newlyTrue`, `newlyInSet`, `crossedThreshold`) plus first-crit / first-dodge / first-cooldown-recharge detectors, consolidating the pattern previously duplicated across `challenge-unlocks.ts` / `synthesis-feedback.ts` / `proficiency-feedback.ts`. This is the mechanic-trigger detection surface, and it is presentation-agnostic — it survives unchanged across the revision.
- **Overlay primitives**: `Popover.svelte` is a non-blocking overlay that covers its nearest positioned ancestor — it is *not* target-anchored, so a point-at-this-control tour is new component work in the same family (shared `focus-trap` chrome), not a straight reuse. The toast store (`toast.svelte.ts`) carries type/dismissible/action, and `requestScreen(key, payload?)` provides one-shot cross-screen deep-linking.
- **#1390 (content export/seed + progression-graph lint) is closed and real** — Workbench-authored reference data with lint coverage is the established, DB-primary content strategy, not an aspiration.
- **Sibling spikes share this event surface**: #1393 (per-page unlock conditions) is a natural future upgrade for "first time this page is reachable"; #1395 (notification badges) watches the same unlock events and wants the same "unread" affordance.

## Directions considered

**Delivery** —
- *Toast-only* (original recommendation): cheapest, but rejected — toasts are already the unlock/reward noise channel, so lessons compete with the feed they're supposed to explain; and in an idle game, mechanic triggers ("first crit") fire within seconds of idling, plausibly while the player is AFK, making fire-and-forget delivery structurally missable.
- *First-visit modal*: unmissable and cheap, but not in-context — it tells rather than shows, and a modal interrupting mid-battle is wrong for mechanic triggers anyway.
- *Anchored popover tour + unread queue* (**chosen**): screen lessons play in-context at the moment the player is provably present; mechanic lessons queue as unread behind a persistent indicator instead of being shown into the void.

**Authoring** —
- *Hardcoded frontend registry* (original recommendation): mirrors `BackgroundThrottleMonitor`, but that precedent is a browser-quirk warning, not game content. The strings were never the issue — the **existence and linkage** of lessons is what the content arc depends on, and hardcoded lessons are invisible to the graph lint and the Workbench.
- *Hybrid* (hardcoded copy, server-side state only): saves the entity/CRUD work but keeps the lint blind spot.
- *Full reference data* (**chosen**): consistent with the DB-primary strategy everything else follows.

**Persistence** —
- *`localStorage`* (original recommendation): device-scoped state for what is account-scoped data; every new device re-prompts everything. The #1395 badge analogy justified accepted data loss for low-stakes badges while conceding tutorials are higher-stakes.
- *Server-side per-player state* (**chosen**): the server already syncs player state; two timestamps per lesson is cheap.

## Decisions

### 1. Trigger model: two anchor kinds, one lesson lifecycle

Every blurb is a **lesson** with a trigger of one of two kinds, and a per-player lifecycle of `locked → unread → read`:

- **Screen-anchored** — fires the first time a given `ScreenDef.key` becomes the active screen, hooked at the same activation point cross-screen navigation uses. Because the player just navigated there, the lesson **plays immediately** (its tour runs, then it's marked read). Once #1393 lands per-screen unlock predicates, this trigger should reuse them (fire when a screen transitions unlocked *and* active); until then "first time ever active" is well-defined on its own.
- **Mechanic-anchored** — fires when a content-events detector (#1586) flips false→true (first crit, first dodge, first cooldown recharge, future unlock events). The player may be AFK or mid-fight, so the lesson does **not** display at trigger time — it becomes **unread**, surfaced by a persistent indicator (a badge on the Help nav entry / HUD affordance, deliberately distinct from the toast channel). Opening it navigates to the lesson's screen via `requestScreen` and plays its tour, marking it read.

"Per-page vs. per-mechanic" is therefore not a fork: two trigger evaluators, one lifecycle, one presentation path. Queue-always for mechanic lessons is a deliberate v1 simplification; playing immediately when the player happens to be on the lesson's screen is a possible refinement, not a requirement.

### 2. Authoring: lessons are reference data

A `Lesson` entity (key, trigger kind + target, host screen, ordered steps of text + optional `anchorKey`, Help-screen ordering) plus a small intrinsic mechanic-event enum, riding the reference-data cache, the #1390 seed/export, and Workbench CRUD like every other content entity. This buys the lint coverage the content arc needs — every taught-by-blurb candidate in content-design.md §2 has a live lesson, and no trigger references retired content — and keeps copy iteration in the Workbench where content iteration already happens. `anchorKey`s are validated on the frontend side (a test that every seeded key resolves to a registered anchor), since the backend lint can't see the DOM.

### 3. Delivery: anchored tour, in v1

A coach-mark tour component (new, in the `Popover`/`ModalHost` overlay family, reusing the `focus-trap` chrome): highlight the target control, position a callout with the step text and next/back/done. Anchors are registered by key via a Svelte action; a missing anchor degrades to a centered callout. Toasts are not used for lessons at all.

### 4. Persistence: server-side player state

A `PlayerLesson` row per player+lesson (absent = locked; `unlockedAt`, `readAt`), included in the player load payload, updated by socket commands (unlock on client-detected trigger, mark-read on tour completion). Client-side trigger detection is trusted — a dishonest client can only show itself tutorials early; nothing is rewarded. Server-side trigger detection is deferred until something rides on it.

## Relationship to #1393 and #1395

- **#1395 (badges)** should consume the same content-events layer (#1586, merged) and can share the unread-indicator affordance this spike introduces, rather than inventing a parallel one — flagged there as a coordination point.
- **#1393 (per-page unlock conditions)** remains the upgrade path for screen-anchored triggers; #1392 doesn't wait for it.

## Implementation issues

- **#1586 — Content-events derivation layer** — merged (PR #1590); survives the revision unchanged as the mechanic-trigger detection surface.
- **#1591 — Lesson reference data** — `Lesson`/`LessonStep` entities, mechanic-event intrinsic enum (+ EF migration), reference cache, seed/export, Workbench CRUD, graph lint rules.
- **#1592 — Anchored tutorial tour component** — coach-mark tour player, anchor registration action, degraded (unanchored) fallback, seeded-anchor resolution test.
- **#1588 — Lesson state persistence (server)** — `PlayerLesson` state, socket commands, player-load payload, client store sync. *(Rescoped from the superseded localStorage approach.)*
- **#1587 — Trigger evaluation + unread lesson flow (client)** — screen-activation hook, content-events wiring, unread indicator, open→navigate→play→mark-read flow. *(Rescoped from the superseded toast approach.)*
- **#1589 — Help screen** — build the reserved `help` slot as the reading room: all lessons with locked/unread/read state, open/replay.

Rough dependency order: #1591 → (#1588, #1592) → #1587 → #1589.

## Documentation to update on landing

- `docs/content-design.md` §2 and the parking-lot entry — drop the "not-yet-built feature" caveat; document the lesson-anchor split so zone-arc planning knows which lane a new lesson uses.
- `docs/frontend.md` — short "Tutorials" note alongside the overlay-systems summary (tour component, anchor action, unread indicator); note the content-events layer for future consumers (#1395).
- `docs/backend.md` — nothing expected; `Lesson`/`PlayerLesson` follow standard reference-data and player-state patterns.

## Out of scope / deferred

- Server-side trigger detection (client-detected, server-recorded is enough while nothing is rewarded).
- Immediate display of mechanic lessons when the player is already on the relevant screen (refinement over queue-always).
- #1393's unlock predicates and #1395's badge system — each its own issue; this doc only defines the shared seams.
