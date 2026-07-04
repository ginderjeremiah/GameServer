# Spike #1392 — UI tutorials (teaching-lane blurbs)

## The question, as filed

"Plan out UI tutorials for each page and develop a robust system for displaying tutorials," with four questions folded in from `docs/content-design.md` §2 (which treats tutorial blurbs as one of two teaching lanes, alongside zone design, for mechanics a player can grasp from one good explanation rather than sustained pressure):

- **Trigger model** — what fires a blurb?
- **Authoring** — reference data (riding #1390's export/seed + Workbench) or hardcoded?
- **Per-page vs. per-mechanic** — the issue frames "tutorials for each page," but content-design.md's named candidates (crit/dodge variance, cooldown charging, idle-loop basics) are mechanic-level, not page-level. Reconcile the two axes.
- **Dismissal/replay** — one-shot or revisitable, and how does dismissal state persist?

content-design.md §2 already calls this "a dependency of the content arc, not just UX polish" — the planned zone arc assumes blurbs exist to carry lessons that don't warrant a whole zone. Its parking lot points back here explicitly, including the reference-data-vs-hardcoded question.

## Current state (what already exists)

No tutorial/blurb system exists today. But three adjacent pieces of infrastructure do, and they change the shape of this spike from "design a system from scratch" to "wire an existing pattern up one more way":

- **Screens aren't routes.** `UI/src/routes/game/screens/` is a config-driven registry (`screen-defs.ts`), not SvelteKit pages — the active screen is local state in `routes/game/+page.svelte` driving a lookup, not a URL. "First visit to a page" therefore can't be `afterNavigate`; it has to hook the same screen-activation point cross-screen navigation already uses. There are 12 built screens today (`fight`, `cardGame`, `challenges`, `inventory`, `skills`, `synthesis`, `attributes`, `proficiencies`, `attributeBreakdown`, `stats`, `codex`, `options`), plus a **reserved-but-unbuilt `help` screen** (`key: 'help', built: false`, settings group) — an already-registered slot with no component yet, and the obvious home for a tutorial replay affordance.
- **A one-shot dismissible notice already exists, and it's a close cousin of a tutorial blurb.** `BackgroundThrottleMonitor` (`UI/src/lib/engine/background-throttle-notice.ts`) watches a domain event (`onIdleTimeLost`) for a mechanic crossing a threshold, persists a single `localStorage` flag (`safeLocalStorage()`, written *before* showing so a crash mid-toast can't re-arm it), and shows a sticky toast (`toastWarning(msg, { duration: 0 })`) exactly once, ever. Its content is a hardcoded string, browser-sniffed even. This is today's only working example of "mechanic happened → one-time educational nudge," and it's built entirely from existing primitives (an engine event hook, `safeLocalStorage`, the toast store) — no bespoke component.
- **A recurring "diff what changed → toast" pattern already exists**, independent of which screen the player is on: `challenge-unlocks.ts` (`zonesUnlockedBy`, `resolveUnlockReward`), `synthesis-feedback.ts` (`isRecipeCreatable`/`creatableRecipeIds`), and `proficiency-feedback.ts` are all pure before/after-state diff helpers feeding a completion toast. This is the natural home for "first crit landed," "first dodge occurred," "cooldown visibly charged" style mechanic triggers — event-driven, not screen-driven, and already decoupled from presentation.
- **The toast primitive already carries most of what a short blurb needs**: type, `dismissible`, an inline `action: { label, onClick }` button, and an `onDismiss` callback (`UI/src/stores/toast.svelte.ts`). Combined with the existing navigation-intent store (`requestScreen(key, payload?)`, one-shot deep-linking consumed via `consumePayload()`), a toast's action button can already say "View" and land the player on the relevant screen. What doesn't exist is an anchored, persistent, in-context explanation (pointing at a specific control on a specific screen) — that would extend `Popover` (`components/Popover.svelte`, the non-blocking anchored-overlay primitive), not the toast.
- **#1390 (content export/seed pipeline + progression-graph lint) is closed and real**, not aspirational — so "ride the Workbench" is a genuinely available option, not a leap. Its own body floated a per-entity `DesignerNotes` field (Workbench-authored, never sent to the client) as a precedent for attaching authored prose to reference entities.
- **Two sibling spikes share this event surface.** #1393 ("unlock conditions for each page") explicitly floats itself as the natural stagger point for tutorials ("this will also provide an opportunity to stagger feature tutorials") — a page's unlock condition flipping false→true is close to a free "first time this page is even reachable" hook. #1395 ("notification badges on unlock") lists proficiency/challenge/item/skill unlock events — the same events a mechanic-triggered blurb would key off — but commits to **client-only, no-recovery** persistence and accepts that as fine for a "come look" badge.

## Decisions

### 1. Trigger model: two anchor kinds, one shared event layer

Model every blurb as a **lesson** with a **trigger**, not "a page's tutorial." A trigger is one of:

- **Screen-anchored** — fires the first time a given `ScreenDef.key` becomes the active screen. Hooks the same activation point `routes/game/+page.svelte` already uses for cross-screen navigation; does not depend on SvelteKit routing. Right for onboarding-shaped lessons content-design.md already named as page-adjacent (idle-loop/UI basics on first landing on Fight).
- **Mechanic-anchored** — fires the first time a pure predicate over player/battle state flips from false to true (first crit landed, first dodge occurred, a skill's cooldown visibly recharged). Detected by extending the existing `*-feedback.ts`/`*-unlocks.ts` before/after-diff family into a small shared **content-events** module, rather than writing a fourth bespoke diff helper. This module is the natural point to also serve #1395's badge triggers — see the open call below.

Both anchor kinds resolve to the same delivery primitive (a toast, optionally with a `View` action via `requestScreen`), so "per-page vs. per-mechanic" isn't a fork to pick a winner from — it's two trigger evaluators feeding one presentation path. A lesson can also declare *both* (e.g. gate the cooldown-charging blurb on having first reached the Fight screen, so it never fires while the player is elsewhere).

Once #1393 lands a per-screen unlock predicate, screen-anchored triggers should reuse it directly (fire the blurb the moment a screen transitions unlocked **and** becomes active) rather than maintaining an independent "has this page been visited" check that can drift from "is this page reachable yet." Until then, screen-anchored triggers degrade gracefully to "first time ever set active," which is already a well-defined, useful trigger on its own.

### 2. Authoring: hardcode the copy, reference-data the trigger

Split the two halves of a lesson rather than forcing an all-or-nothing call:

- **Trigger condition** (screen key, or a mechanic id referencing existing ids — an attribute, a skill, an activity key) needs no new authored entity. It's expressed by referencing content that's already reference data.
- **Blurb copy** is hardcoded frontend content — a single lesson registry (`.ts`, mirroring `backgroundThrottleGuidance()`'s shape but as static per-lesson strings rather than one browser-sniffed function). Tutorial copy is UI microcopy iterated alongside the frontend, not a cross-referenced content-graph node needing #1390's reachability lint, diffable seed export, or Workbench round-trip. This mirrors the one precedent that exists today (`BackgroundThrottleMonitor`'s hardcoded, non-authored message) rather than introducing a new Workbench entity + DB round-trip for a few dozen short strings.

This isn't a permanent wall — if the lesson roster grows large enough to want lint coverage (e.g. "every content-design.md blurb candidate has a live trigger, and no trigger references a retired lesson"), that can ride the existing progression-graph lint as a follow-on, keyed off the same trigger ids. Not core to landing the feature.

### 3. Per-page vs. per-mechanic: resolved by decision 1

Not a fork — see above. Every lesson has an anchor; "page" and "mechanic" are two anchor kinds feeding the same trigger→toast pipeline. content-design.md's phrasing ("tutorials for each page") describes the screen-anchored subset, not the whole system.

### 4. Dismissal/replay: one-shot locally, always-available via Help

Dismissal state is `localStorage`-backed (`safeLocalStorage()`), one entry per lesson id, mirroring `BackgroundThrottleMonitor` and consistent with #1395's own accepted client-only/no-recovery stance for the same event surface.

This is only defensible for tutorials — a strictly higher-stakes case than a badge — if losing the flag has a low-stakes failure mode. It does: build out the already-reserved, currently-stubbed **`help` screen** (`screen-defs.ts`, `built: false` today) as a plain list of every lesson (dismissed or not), so a cleared `localStorage` or a first-time load never means "this explanation is gone" — worst case the player sees a blurb again, or has to open Help to find something they'd already dismissed. That reframes local-only persistence from "accepted data loss" (as #1395 explicitly is) to "harmless re-prompt," which is the right tradeoff for a teaching mechanism without taking on a server round-trip (`logPreferences`/`SaveLogPreferences` is the available precedent if that judgment ever changes, but nothing here calls for it).

## Relationship to #1393 and #1395

All three issues ultimately watch the same underlying events (page reachability, mechanic/unlock occurrence). This spike's recommendation:

- #1392 (this spike) owns the **content-events** derivation layer (extending the `*-feedback.ts`/`*-unlocks.ts` pattern) as part of its own implementation, since it needs it regardless.
- #1395 (badges) should consume the same layer rather than re-deriving "did X just unlock" a second time — flagged there as a coordination point when it's picked up, not decided unilaterally here since #1395 is separately scoped and owned.
- #1393 (per-page unlock conditions) is the eventual upgrade path for screen-anchored triggers (see decision 1); #1392 doesn't need to wait for it to ship a first version.

## The content-events scoping call — resolved

Building the shared content-events layer as part of #1392 was flagged as a sequencing/ownership call rather than decided unilaterally. Resolved via PR #1585 review: keep it under #1392 as proposed, rather than splitting it into a standalone dependency issue — it's the same code either way, and the split only pays off if #1395 gets picked up before #1392 ships, which isn't the current ordering.

## Implementation issues

- **#1586 — Content-events derivation layer** — shared before/after-diff module for mechanic-occurrence detection (crit landed, dodge occurred, cooldown charged, proficiency/skill/item/challenge unlocked), reusing/consolidating the existing `challenge-unlocks.ts`/`synthesis-feedback.ts`/`proficiency-feedback.ts` family. Pure, unit-testable, no UI.
- **#1587 — Lesson registry + trigger evaluation** — the static lesson list (id, anchor kind + target, copy, `View` action target), a screen-activation hook for screen-anchored triggers, and wiring content-events output into mechanic-anchored triggers. Delivers via the existing toast store; no new presentation component required for v1.
- **#1588 — Dismissal persistence** — `localStorage`-backed per-lesson dismissal, mirroring `BackgroundThrottleMonitor`'s write-before-show ordering.
- **#1589 — Help screen** — build out the reserved `help` screen key (`built: false` today) listing every lesson with its dismissed/undismissed state and a way to re-trigger its toast.

## Documentation to update on landing

- `docs/content-design.md` §2 and the parking-lot entry — drop the "not-yet-built feature" caveat once landed; document the lesson-anchor split (screen vs. mechanic) so future zone-arc planning knows which lane a new lesson can use.
- `docs/frontend.md` — add a short "Tutorials" note alongside the overlay-systems summary, pointing at `docs/frontend-overlays.md` for the toast mechanics reused and noting the content-events layer's existence for future consumers (e.g. #1395).

## Out of scope / deferred

- Workbench-authored blurb copy (revisit only if the lesson roster grows enough to want lint coverage — see decision 2).
- Server-persisted dismissal state (revisit only if local-only proves insufficient in practice — see decision 4).
- Anchored/in-context overlays pointing at a specific control (a `Popover`-based presentation beyond the toast); v1 ships toast-only.
- #1393's per-page unlock predicate and #1395's badge system themselves — each is its own spike/issue; this doc only defines the shared seam.
