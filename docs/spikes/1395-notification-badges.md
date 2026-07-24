# Spike #1395 — notification badges for unlocks

## The question, as filed

"Add notification badges to relevant pages when things are unlocked," naming four examples — a
proficiency unlocked/leveled up, a challenge completed, an item unlocked, a skill unlocked — with
three constraints already stated in the issue: best-effort (lean toward not annoying the player),
probably client-side only, and no recovery feature for a badge lost to e.g. a cleared `localStorage`.

## Current state (what already exists)

- **Three of the four named events already toast, but only while the tab is visible.**
  `challengeCompletedMessage` (challenge completed, naming its item/mod reward),
  `proficiencyLevelMessage`/`proficiencyMilestoneMessage`/`proficiencyOpenedMessage` (proficiency
  level/milestone/newly-opened, milestone naming any granted skill), and `recipeAvailableMessage`
  (a synthesis recipe becoming craftable) are all wired in `lib/engine/engine.ts` through a shared
  `toastIfVisible` helper (`engine.ts:89-94`) that **drops the toast outright** — no queue, no
  replay — when `document.hidden`. This is the deliberate `#1621` convention documented in
  `frontend.md` ("presentational side effects... are dropped, never queued or replayed"), and it
  means the exact scenario this issue wants a badge for — the player was away/idling and missed the
  notice — is not a hypothetical, it is the confirmed, everyday behavior of the existing toast path
  for an idle game where the tab is often unfocused.
- **A working nav-badge precedent already ships**, built for tutorial lessons (#1392/#1587):
  `NavSidebar.svelte` renders a `.unread-badge` pill on a screen's rail entry, today hardcoded to
  `screen.key === 'help'`, sourced from `playerManager.lessons.filter(l => !l.readAt).length` —
  server-side per-player state. The visual affordance and its motion/styling are done; what's
  hardcoded is the single screen key and the server-backed data source.
- **The content-events derivation layer (#1586, merged)** — `lib/common/content-events.ts` — was
  built with this issue in mind (its file header names "#1395's badges" as a future consumer
  alongside tutorials) and already exports the generic primitives this needs: `newlyTrue` (a flip
  false→true), `newlyInSet` (ids present in an after-set but not a before-set — exactly "what
  newly unlocked"), and `crossedThreshold` (a level/milestone crossing). Today it is only actually
  consumed by the tutorial mechanic triggers (`isFirstCrit`/`isFirstDodge`/`isFirstCooldownRecharge`,
  all battle-tick-scoped); the three existing toast call sites in `engine.ts` each do their own
  inline before/after diff at the push-handler level rather than routing through these primitives —
  so the "one shared derivation layer" promise in `content-events.ts`'s own header is only half
  fulfilled today.
- **"Item unlocked" has no independent event.** Per `game-design.md`, items and mods unlock only
  through challenge completion — there is no separate item-unlock push. `challengeCompletedMessage`
  already names the rewarded item/mod. So the issue's four named events are really three detection
  surfaces (proficiency, challenge/item, skill), not four.
- **"Skill unlocked" is only partially covered by existing detection.** A milestone-granted skill is
  named inside the proficiency-milestone toast today. A **synthesized** skill is not toasted at all —
  `synthesis-feedback.ts`'s header states this is deliberate ("synthesizing itself is player-driven
  and deliberately un-toasted"), since the player just clicked "synthesize" and is looking at the
  result. An **item-granted** (innate) skill has no unlock moment in the same sense — it's present
  whenever the granting item is equipped, not a one-time event.
- The four screens the issue implies — Proficiencies, Challenges, Inventory, Skills — all exist and
  are registered in the screen registry (`screen-defs.ts`), so there's a real nav entry to badge for
  each.

## Directions considered

**Badge granularity** —
- *Nav-level aggregate count* (mirrors Help's existing badge): one small generalization of
  `NavSidebar.svelte`'s hardcoded `screen.key === 'help'` check into a per-screen unread count. Cheap,
  visually consistent with the one badge affordance players already see, and needs no changes to any
  in-screen list component.
- *Per-item "NEW" tags* inside each screen's list (a highlighted proficiency node, challenge row,
  inventory item, or skill entry): a more legible signal — it says *which* thing is new, not just
  that something is — but touches every list-row component across four screens and needs its own
  "mark seen" trigger (scroll into view? screen visit? explicit dismiss?) rather than the one clean
  "opening the lesson marks it read" moment tutorials had.
- Recommendation below is nav-level first; per-item tagging is a legitimate v2 once the underlying
  "what's new" plumbing exists and is proven, not a reason to block v1.

**Persistence** —
- *Client-side only* (`localStorage`, as the issue itself proposes): the issue explicitly accepts
  losing badge state to a cleared browser store, with "no recovery feature." This is the same shape
  #1392's first draft proposed for lesson dismissal — and that draft was reversed after collaborator
  review specifically because lesson state is account-scoped and content-arc load-bearing (a lost
  "read" flag would silently re-run tutorials on every new device, and #1393/future gating might key
  off it). **Badge "seen" state carries neither risk**: it gates no content, has no server-side
  consumer, and losing it just means a badge that should be gone reappears (or one that should show
  doesn't) — a cosmetic nuisance, not a correctness or progression problem. So unlike lessons, the
  issue's own client-only framing holds up under the same scrutiny that overturned it there.
- *Server-side* (mirroring `PlayerLesson`): would give cross-device consistency and a "true" no-loss
  guarantee, but adds a persistence surface (entity, migration, socket commands, load-payload growth)
  for state that, per the above, doesn't need that guarantee. Not recommended unless the maintainer
  wants badge state to double as a foundation for something server-visible later.

**Detection wiring** —
- Reuse the three existing toast call sites' diff points rather than building a parallel "did this
  change" pass — they already run at exactly the right moments (challenge completion, proficiency
  push, synthesis-availability check). Whether to also migrate their inline before/after comparisons
  onto `content-events.ts`'s `newlyTrue`/`newlyInSet`/`crossedThreshold` (closing the "half-fulfilled
  shared layer" gap noted above) is a small, separable cleanup worth doing alongside this, not a
  prerequisite for it.
- A badge counter needs its own client-side "last-seen watermark" per source (e.g. last-seen
  proficiency level per proficiency, last-seen challenge-completed count, last-seen owned-skill set)
  to compute "unread since I last looked" — this is new state the toast path never needed, since a
  toast has no concept of being revisited.

## Recommendation (pending maintainer sign-off)

Ship v1 as **nav-level aggregate badges**, generalizing `NavSidebar.svelte`'s existing `.unread-badge`
pattern from its one Help-only special case to a small per-screen unread-count map, for the
Proficiencies, Challenges, and Skills nav entries (Inventory's "item unlocked" collapses into the
Challenges badge, since completing a challenge is the only way an item unlocks — see above). Persist
the "last seen" watermarks **client-side only**, per the issue's own framing, since badge state is
cosmetic and gates nothing. Hook detection at the existing toast call sites in `engine.ts` (folding
their inline diffs onto `content-events.ts`'s shared primitives where it's a clean win) rather than
building a second derivation pass, and clear a screen's badge the same way Help's does — by viewing
that screen. Treat per-item "NEW" tags as a natural, separately-scoped v2 once this plumbing exists,
not part of this pass.

Not filing implementation issues yet — per this repo's spike convention, that follows a collaborator
pass on the open questions below.

## Open questions for the maintainer

1. **Nav-badge-first, or per-item tags from the start?** The recommendation above is the smaller,
   precedent-matching v1; if per-item "NEW" tags are the more valuable signal to you, that's a
   materially larger first PR (four list components, not one nav generalization).
2. **Is client-side-only persistence actually fine here**, or would you rather badge state ride the
   same server-side pattern lessons ended up on (for consistency, or because you want it visible to
   more than one device)? The case above is that it's lower-stakes than lesson state, but that's a
   judgment call worth your explicit sign-off given #1392's precedent of reversing the same call.
3. **Should a synthesized skill start getting a badge/notification?** It's currently deliberately
   un-toasted (the player is already looking at the result when they click synthesize); adding badge
   coverage for it would be new behavior, not just closing an existing gap.
4. **Does Inventory get its own badge**, distinct from Challenges, or is "a challenge completed"
   sufficient signal and a separate "new item in your inventory" badge redundant? Leaning toward the
   latter (see Detection wiring), but naming it explicitly since the issue lists item and challenge
   as two separate bullets.

## Relationship to #1392 and #1586

This spike's detection layer is the consumer #1586's header predicted. No code changes are proposed
in this PR — this is the research pass; a follow-up issue (or issues) scopes the actual
implementation once the above gets a look.
