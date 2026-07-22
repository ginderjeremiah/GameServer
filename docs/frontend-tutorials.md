# Frontend Tutorials (lessons & coach-mark tours)

The tutorial system's frontend mechanics, split from [frontend.md](./frontend.md#tutorials) so the main doc stays an overview. The backend half (the `Lesson` reference set and the player-owned read state) is in [backend.md → Tutorial lessons](./backend.md#tutorial-lessons-reference-set--player-owned-read-state).

A tutorial blurb (spike #1392) is a `Lesson` — Workbench-authored reference data with an ordered set of steps, each optional-`anchorKey` + text — presented as an anchored coach-mark tour (`components/tour/TourPlayer.svelte`), never a toast.

- **Two trigger kinds, both evaluated client-side** in `lib/engine/tutorials.ts` (there is no server push):
  - **Screen-anchored** — fires the first time its host screen becomes active, hooked at the single screen-activation point in `routes/game/+page.svelte` (screens are registry state, not routes), and plays immediately — unless another tour is already playing (e.g. one just manually opened from the Help screen), in which case it queues as unread instead of replacing the active tour (#2269).
  - **Mechanic-anchored** — fires on a content-events detector flipping false→true inside `BattleEngine.logicalUpdate`'s per-tick activations; queues as **unread** rather than displaying immediately, since the player may be AFK.
- **Per-player lifecycle.** A `PlayerLesson` (`playerManager.lessons`) tracks `locked → unread → read`; the unread count surfaces as a persistent badge on the sidebar's Help entry, distinct from the toast channel.
- **One shared open/read flow.** Opening a lesson (from a trigger, or manually) goes through `openLesson`/`closeTutorialTour`: navigate to the host screen via the `navigation` store, play the tour, and mark it read on either completion or early dismissal (Skip means "got it", not "remind me later").
- **Anchors are keys, not selectors.** Tour anchor targets register by a stable string key via the `tutorialAnchor` action rather than a DOM selector, so a step whose anchor is missing degrades to a centered callout instead of failing.
- **The content-events derivation layer is reusable.** `lib/common/content-events.ts` is presentation-agnostic and reusable by future consumers (e.g. notification badges) beyond tutorials.
