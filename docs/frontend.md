# File Structure

All frontend code lives in `UI`:

- **`src`** — Svelte components, stores, and app code.
  - **`components`** — shared components, including reusable building blocks worth composing over re-implementing: the collapsible side rails (`components/sidebar`), the tooltip chrome (`components/tooltip` — `TooltipShell` plus section/title/stats primitives and their sealed/teaser counterparts), `AttributeTooltip` (drive it through `createAttributeTooltip()`; publish the controller via context for descendant hover triggers), and the progress/fill `Bar` (the track+fill+ARIA `progressbar` primitive — supply its treatment via `--bar-*` CSS vars and overlay extra marks through its `children` snippet; `HpBar` stays the specialised health variant with its disappearing layer, centred text and phase pips). Extend these primitives rather than copying them.
  - **`lib`** — non-UI logic: `api` (HTTP/WebSocket client + generated client types), `battle` (battle-sim domain objects + the shared domain mirrors backing the breakdown/inventory screens), `card-game` (pure "Initiative Loom" mechanics, DOM-free), `common` (shared utilities), `engine` (render/logical engines, battle engine, player/enemy managers).
  - **`routes`** — pages and route-specific components. In-game screens under `routes/game/screens` use a config-driven **screen registry** (`screen-defs.ts`); adding a screen means registering it there. Per-screen detail lives in [frontend-screens.md](./frontend-screens.md).
  - **`stores`** — Svelte stores. NOTE: most stateful data is moving to reactive classes in `lib` made reactive via `statify`; prefer a reactive class over a new store.
  - **`styles`** — global styles and theming.
  - **`tests`** — unit tests for lib/pages/components.
- **`static`** — assets. Item/skill icons live in `static/img` — see [icon-art.md](./icon-art.md) before creating or regenerating any.
- **`e2e-tests`** — Playwright end-to-end tests.

# General Frontend Guidelines

- There is ongoing work both to reach feature parity with the old frontend and to redesign the UI/UX; not all new UI/UX decisions are final.
- Keep components small. If a component grows large or you find yourself commenting each section, break it into subcomponents (in their own folder if there are several), even if only the parent uses them.
- Use [statify](..\UI\src\lib\common\statify.svelte.ts) to make a logic class reactive in Svelte — it keeps complex state/logic (e.g. battle simulation) in object-oriented classes while staying reactive, rather than pushing it into stores or components.

## Testing Guidelines

Unit-test all UI logic (pages/components) and lib code; simple non-interactive display components don't need tests. Place tests under `tests` mirroring the source structure. Use Playwright e2e tests only for critical user flows, written from the user's perspective — they're costly to write and run.

# Important Architectural Design Decisions

## Battle simulation & parity

The frontend simulates battles in real time as anti-cheat: the backend replays the battle the client reports, so the two simulators must agree tick-for-tick (see [game-design.md](./game-design.md) and the backend parity notes). The per-tick arithmetic has a **single source** — the pure `battleStep` (`$lib/battle/battle-step.ts`): the player's ready skills fire, then (only if the enemy survives) the enemy fires back, operating on real `Battler`/`Skill` objects. Two consumers drive that one stepper: `BattleEngine.logicalUpdate` (the live render-driven loop, layering combat-log/stage UI on top) and `BattleSimulator` (a headless runner, the analogue of the backend's `BattleSimulator`).

The parity suite drives the production `BattleSimulator` (not a hand-rolled copy), so a divergence from the backend fails parity rather than silently weakening the replay; keep its scenario matrix row-for-row aligned with the backend's.

**The scalar formulas are shared with the display surfaces.** A skill's raw damage, the flat-Defense subtraction (clamped at zero), and the cooldown multiplier live as pure functions in `$lib/battle/battle-formulas.ts`; the battle classes delegate to them, and any UI showing battle numbers (skills page, skill tooltips) must consume the same functions rather than keeping a display-only copy — so a formula change propagates everywhere and stays under the parity suite's guard.

**Mid-battle attributes are a modifier graph, not a frozen snapshot.** `BattleAttributes` retains its modifier list instead of flattening to numbers at battle start, composing per-attribute totals through the same `computeAttributes` path the breakdown screen uses (memoised, recomputed only on add/remove). This mirrors the backend `AttributeCollection`, so both sides resolve derived attributes through one composition path even as values change mid-fight — the surface skill effects build on.

## Authentication

The frontend authenticates against the backend's JWT scheme (see `backend.md` for the server side). All token handling lives in the API client layer (`lib/api`) so the rest of the app never touches tokens:

- **`token-store.ts`** is the single source of truth for credentials — it persists the `{ accessToken, refreshToken }` pair in local storage (surviving a refresh) and decodes the access token's `exp` for pre-emptive refresh.
- **`auth.ts`** owns the refresh lifecycle and **collapses concurrent callers onto a single in-flight refresh** — refresh tokens are single-use on the backend, so parallel refreshes would consume the token twice and log the user out. It uses raw `fetch` to stay out of the 401-retry path and avoid an import cycle.
- **`api-request.ts`** attaches the bearer header, refreshes pre-emptively, and on a 401 refreshes once and retries. The anonymous auth endpoints are exempt.
- **`api-socket.ts`** passes the access token as a query param (browsers can't set WebSocket handshake headers) and, on an auth-rejected handshake, refreshes and reconnects (bounded — see _Socket connection lifecycle_).

**Role-based access checks.** The access token also carries the user's `role` claim(s); `hasRole(ERole.Admin)` is the single predicate the UI gates on (`ERole` mirrors the backend and is hand-maintained, not codegen'd). The sidebar hides role-gated screens and the `/admin` route self-guards on mount — but this is **UX only**; the backend independently enforces the Admin role on every admin endpoint. The generated API types (`lib/api/types`) come from the backend's codegen — regenerate them rather than hand-edit (see [infrastructure.md](infrastructure.md#standalone-typescript-codegen)).

## Socket request lifecycle

Each sent command is tracked in an in-flight map keyed by command id, pruned the moment its response resolves. The transport signals **every** failure — a server error, a dropped connection, or a per-request timeout — by **resolving** the promise with a response carrying an `error` field, never by rejecting; callers inspect `response.error` rather than wrapping in try/catch. When the socket closes, still-pending requests are settled this way instead of hanging, but in-flight commands are deliberately **not** blind-resent on reconnect (only the not-yet-sent queue is flushed) — commands like `DefeatEnemy` aren't idempotent and the lost response may well have been a success.

A **per-request timeout** (30s) backstops the one case the close handler can't: the socket stays open but no reply arrives. It is armed only when a command is actually sent (not while it's queued waiting out a reconnect) and cleared on a real response. The cap is deliberately generous so legitimately-slow commands aren't spuriously failed.

## Socket connection lifecycle

The single module-singleton socket doubles as a background reconnect: a 10s keepalive ping (also used for latency) re-arms a dropped socket while idle. Because the singleton outlives any one connection, the lifecycle is torn down deliberately:

- **The keepalive is owned, not fire-and-forget** — its handle is cleared on a clean close, an unrecoverable auth failure, and an explicit `disconnect()`, and the ping self-terminates if it fires with no stored token, so it can't resurrect a socket for a logged-out user. `disconnect()` is called from the `SocketReplaced` teardown (which navigates client-side without a reload, so the ping would otherwise reconnect and fight the session that just took over).
- **Auth-rejection retries are bounded** (5 in a row) so a flapping server can't drive an unbounded refresh/reconnect loop that burns single-use refresh tokens. The budget refills only after a reconnect stays open a stable interval, so a socket that opens then is immediately closed doesn't reset the count every cycle.

## Reference Data

Reference data is fetched from the backend on startup and cached in-memory (`$stores/static-data`) for fast access (items, mods, skills, enemies, zones, challenges). It is additionally cached in **persistent browser storage** so a refresh re-downloads only the sets that changed: on boot the loading screen fetches a content hash per set, re-fetches only the sets whose hash differs from the cached copy, and hydrates the rest from `localStorage`. The reference-data table and its version/cache plumbing live in `$lib/engine/reference-data.ts` (the `localStorage` I/O in `reference-cache.ts`) because they're shared by the loading screen and the silent session-resume path.

**Lookup convention — index by id for the zero-based-id catalogues, `.find` for everything else.** The six zero-based-id sets (items, itemMods, skills, enemies, zones, challenges) are resolved by **array index** (`staticData.items?.[id]`), mirroring the backend and honouring the Id-as-index invariant retirement preserves (a retired record keeps its slot, so the index never shifts — see `backend.md` → _Reference Data_). This is O(1) and is the single convention for these sets; don't mix in `.find(e => e.id === id)`. The `.find` form is reserved for the non-index-addressable sets: the enum-keyed intrinsic sets (attributes, challengeTypes) and the `.find`/Map-keyed tags.

## Admin Tools: the entity-driven Workbench

The admin tools (`src/routes/admin`) are a single config-driven **Workbench** (`src/routes/admin/workbench`): a master–detail page with a searchable record list and a tabbed detail panel, under one catalogue-wide "Save Changes" bar. It replaced a separate page per operation (which forced re-selecting the same entity in each).

- **One component, many entities.** A single `Workbench.svelte` renders any entity described by an `EntityConfig` (`workbench/entities/*.ts`) — list metadata plus an ordered list of typed **sections** (`fields`/`table`/`chips`/`tags`/`usage`/`challenge-condition`/`challenge-reward`). Adding an admin entity is a new config object, not new UI. When a section needs logic the generic kinds don't cover it gets its own `kind` (rendered by `SectionRenderer`) rather than forking the Workbench, kept generic via two opt-in hooks (a one-line `headline` preview and a `dirtyKeys` declaration so a custom section still shows the unsaved dot).
- **Diffing store.** `EntityStore` keeps an editable copy plus a baseline and derives per-record status (added/modified/deleted/clean) by structural comparison — the same dirty-tracking model as the generic `TableEditor`, generalized to whole records. The save bar shows live counts; Discard reverts to baseline.
- **Validation lives where it occurs.** A field is `required` or a non-field section carries a `warn(record)` predicate; the list row, the offending tab, and the field all surface the same warning. The amber **dot** is reserved for unsaved/dirty state and the amber **triangle** for a validation warning — the two never overlap.
- **Save orchestration.** "Save all" commits every pending change. Because the backend persists each relationship through its own endpoint, the save first posts the identity-level Add/Edit/Delete batch, refetches to resolve the ids of newly-added records (the backend appends adds in send order, so the k-th add maps to the k-th new id), then runs each section's child-saver against the resolved id. Each save calls only the endpoints it needs — a record is `modified` when any part differs from baseline, so the identity Edit is sent only when the primary DTO changed and each child-saver no-ops when its collection is unchanged.
- **Reference reads go over the socket; only writes use HTTP.** The catalogues and select-option data load via the same `Get*` socket commands the loading screen uses (through `fetchSocketData`, which throws on a socket error so failures surface as toasts); the persistence calls stay on HTTP. Freshness after an edit is server-side: every admin write reloads the in-memory reference caches, so the post-save refetch serves fresh data. The only reference data still on HTTP is what has no socket command yet (tags).
- **Retired records are excluded from authoring pickers, but never silently dropped.** A retired reference record (see `backend.md` → _Retiring reference data_) stays resolvable by id but is out of circulation, so the select-option providers **omit it from new authoring — a hard block, not a warning**. The one exception is the value a row already references: a retired *current* value is kept in the list with a "retired" affordance, and once the author changes away from it it can't be re-picked. Index-based display lookups stay unfiltered so a retired reference always renders its name.

## Toast notifications

User-facing notifications (currently errors; the system is type-aware for future success/warning/info) are a single global toast system. The `toast` store owns the active toasts in a keyed map (mirroring the tooltip store, so dismissal is order-independent) and exposes `showToast` plus per-type helpers; a single `ToastContainer` in the root layout renders the stack and the individual `Toast` components are presentation-only. The visual treatment derives from a single per-type `--toast-accent` token so a theme can restyle a status by overriding that one variable.

- **The store is the single entry point** — anywhere imports a helper and calls it; error sources are wired to the store rather than rendering ad-hoc UI (socket failures are subscribed once in the root layout; the Workbench reports save/load failures that were previously swallowed).
- **Duplicate messages collapse** — a repeated identical message refreshes the existing toast's timer instead of stacking, so a flapping connection can't bury the screen.
- **Auto-dismiss is opt-out** — pass `duration: 0` for a sticky toast.
- **The store removes synchronously; the view animates the exit.** Dismissal deletes from the store immediately (logically gone), but `ToastContainer` keeps its own render list and lets a removed toast linger marked `leaving` until its CSS exit animation finishes — so the motion lives in CSS and collapses under `prefers-reduced-motion`, while the store stays the single synchronous source of truth.

(An optional inline `action` button and an `onDismiss` callback are part of the standardized design and provided for future use.)

## Modal dialogs

Blocking confirmation dialogs are the toast system's counterpart, from the same standardized design. The `modal` store is the single entry point; a single `ModalHost` in the root layout renders the active dialog, and the presentation-only `Modal` flexes across `confirm`/`acknowledge`/`destructive` kinds.

- **Promise-based, await-able API.** `showModal(options)` (and the `confirmModal`/`acknowledgeModal`/`dangerModal` helpers) returns a `Promise<boolean>`, so a caller writes `if (await confirmModal({ … })) { … }`. Used by the game's quit control and the `SocketReplaced` handler (which, when the player's one allowed connection is taken over, stops the engines and acknowledges before returning to login via a client-side navigation — not a reload, which would bounce a still-authenticated client back through the loading screen).
- **Two sides of the single-connection model.** The `SocketReplaced` handler is the *losing* session's experience; the *winning* side is warned **before** taking over — on login, `confirmSessionTakeover()` calls the authenticated `Login/ActiveSession` endpoint (over HTTP, so it never opens a socket of its own) and confirms if the player already has a live connection elsewhere. It fails open (a non-200 proceeds rather than blocking a legitimate login) and runs only on explicit login, not the silent resume path.
- **A queue, not a single slot** — only the head is shown, but requests queue (each with its own resolve) so two callers each get an answer.
- **Dismissal semantics depend on kind** — backdrop-click/Escape are a cancel for `confirm`/`destructive` but an acknowledgement for `acknowledge`.
- **The host owns the interaction model so the card stays presentational** — backdrop/Escape dismissal, a focus trap, focus capture+restore, and a scroll lock; it focuses the safe action for a destructive dialog (so a stray Enter can't confirm) and the primary action otherwise. All motion is CSS so `prefers-reduced-motion` collapses it.

**Non-blocking overlays compose `Popover` (`components/Popover.svelte`), not a hand-rolled backdrop.** It is the `ModalHost` counterpart for overlays that aren't a blocking confirm — driven by a plain `open` boolean + `onClose` rather than the promise queue — and owns the same chrome: backdrop/Escape dismissal, a focus trap, focus capture+restore, and a body scroll lock, with the content passed as a snippet. The layer is absolutely positioned, so it overlays its nearest positioned ancestor (mount it inside a `position: relative` container) rather than the whole viewport.

## Accessibility

Interactive controls are real elements, not `role="button"` divs — convert clickable `div`/`span`s to `<button>` (the app is mid-conversion; some `role="button"` tiles remain, e.g. `skills/EquippedBand.svelte`).

**Accessible card pattern.** When a tile must host its own nested actions (favorite/unequip) and/or be a drag source, it can't itself be a `<button>` — nesting interactive elements is invalid HTML. Instead it uses a presentational container with a full-bleed primary-action `<button>` (`inventory/OverlayButton.svelte`) stretched beneath the secondary action buttons, which sit at a higher `z-index` so they stay clickable. The overlay carries the drag handle and gets native keyboard activation — modifier state rides the synthesized click, so a ⌘/Ctrl-activate can branch (e.g. select vs. equip) without a manual `keydown`. A card with no primary action (an empty equip slot) renders no overlay, so it stays out of the tab order while remaining a drop target. Hover-only affordances on the presentational container (tooltips, drop highlighting) carry a `<!-- svelte-ignore a11y_no_static_element_interactions -->`.

## Styling

Most styling lives in each component's scoped scss; global styles and theming live in `UI/src/styles`. **All colours are CSS variables, used by semantic intent — never hard-coded.** Core colours are declared in `+layout.svelte` (pulled from `_colors.scss`); the palette is intentionally limited for consistency and contrast. The frontend is built for future custom themes that override those variables, so any colour must be a variable (added to `+layout.svelte`) and used according to its semantic meaning, even when two roles happen to share a hue.

The sub-patterns below all follow that rule:

- **Canvas backstop (white-flash fix).** The visible surface is `.page-base`'s gradient (a transparent-background `background-image`); `common.scss` sets an opaque dark `background-color` on `html, body` so a solid colour paints on the first frame, removing the white flash on routes that render nothing until a client-only gate resolves (`/admin`). The themeable surface stays `.page-base`.
- **Reduced motion.** `common.scss` honours `prefers-reduced-motion: reduce` by collapsing transitions/animations to near-instant (kept non-zero so `transitionend`/`animationend` still fire). Beyond accessibility, this is the root-cause fix for motion-driven Playwright flakiness (the e2e config sets `reducedMotion: 'reduce'`) — prefer it over per-test waits when an animation makes an element unstable.
- **Scrollbars.** `common.scss` sets an app-wide themed scrollbar from the `--scrollbar-thumb`/`-hover` tokens, picked up automatically — don't re-declare per screen. A container needing different geometry should override only that and keep pulling colour from the shared tokens.
- **Rarity palette.** Per-tier hue (`--rarity-*`) and glow intensity are CSS variables read only through the `$lib/common/rarity` helpers (`rarityColor`/`rarityGlow`/`rarityTint`/…). Components use these rather than hard-coding rarity hex, so a theme restyles every rarity surface via the variables alone.
- **Themeable accent palettes & `tintColor`.** The rarity pattern is the template for every data-driven accent — challenge-type, item-category, and item-mod-type accents are all CSS variables read only through `$lib/common` helpers. The generic `tintColor(color, alpha)` produces a theme-overridable `color-mix` for any colour; prefer `var(--…)` + `tintColor` over hard-coded rgba.
- **Tooltip surface.** The shared tooltip container paints its panel with the themeable `--tooltip-bg` (a `color-mix` over `--surface` whose tint is the one see-through knob, plus a backdrop blur); item/mod tooltips inherit it. Item tooltips accent the left border by rarity and the category row by category; the displayed name is composed from applied mods and the stats reflect merged item+mod attributes.
- **Editor change-state accents.** A pending add/edit/remove in the admin editor uses a dedicated `--change-added`/`-modified`/`-removed` token trio, deliberately separate from `--accent`/`--warning`/`--enemy-accent` even though they currently resolve to the same hues — a row marked for removal is not an "enemy", and an unsaved edit is not a validation "warning". Use these for change-state UI and reserve `--warning` for validation.

## In-game screen design notes

Per-screen design decisions live in **[frontend-screens.md](./frontend-screens.md)**. When you add a new in-game screen, document its notable decisions there — surface a decision in this file only if it establishes a cross-cutting pattern other screens are expected to follow.

## Claude Design files

Most redesigned screens were crafted in the Claude Design tool; the chats and handoff artifacts are listed in **[designs.md](./designs/designs.md)** for context on the initial designs.
