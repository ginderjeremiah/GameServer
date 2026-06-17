# File Structure

All frontend code lives in `UI`:

- **`src`** — Svelte components, stores, and app code.
  - **`components`** — shared components. Several reusable primitives are worth composing over re-implementing — the collapsible side rails (`components/sidebar`), the tooltip chrome (`components/tooltip`, incl. `AttributeTooltip`), and the progress/fill `Bar` (with specialised variants like `HpBar`). **Extend an existing primitive rather than copying it**, and check here before hand-rolling a new one; each primitive's own API lives in its code/props.
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
- Use [statify](../UI/src/lib/common/statify.svelte.ts) to make a logic class reactive in Svelte — it keeps complex state/logic (e.g. battle simulation) in object-oriented classes while staying reactive, rather than pushing it into stores or components.

## Testing Guidelines

Unit-test all UI logic (pages/components) and lib code; simple non-interactive display components don't need tests. Place tests under `tests` mirroring the source structure. Use Playwright e2e tests only for critical user flows, written from the user's perspective — they're costly to write and run.

# Important Architectural Design Decisions

## Battle simulation & parity

The frontend simulates battles in real time as anti-cheat: the backend replays the battle the client reports, so the two simulators must agree tick-for-tick (see [game-design.md](./game-design.md) and the backend parity notes). The per-tick arithmetic has a **single source** — the pure `battleStep` (`$lib/battle/battle-step.ts`): the player's ready skills fire, then (only if the enemy survives) the enemy fires back, operating on real `Battler`/`Skill` objects. Two consumers drive that one stepper: `BattleEngine.logicalUpdate` (the live render-driven loop, layering combat-log/stage UI on top) and `BattleSimulator` (a headless runner, the analogue of the backend's `BattleSimulator`).

The parity suite drives the production `BattleSimulator` (not a hand-rolled copy), so a divergence from the backend fails parity rather than silently weakening the replay; keep its scenario matrix row-for-row aligned with the backend's.

**The seeded battle RNG is a parity surface too.** One `Mulberry32` — seeded from `enemyInstance.seed` in `BattleEngine.reset`, and from the headless `BattleSimulator`'s seed — is threaded into `battleStep`, where the player-only crit/dodge/block rolls draw from it in a fixed, outcome-independent order (1 crit draw per player fire, then dodge+block per enemy fire). Both sides must seed identically and draw in the same order or every fight diverges; see [backend-battle.md](./backend-battle.md#battle-runtime--seeded-crit--dodge--block-player-only) for the rules.

**The scalar formulas are shared with the display surfaces.** A skill's raw damage, the flat-Defense subtraction (clamped at zero), and the cooldown multiplier live as pure functions in `$lib/battle/battle-formulas.ts`; the battle classes delegate to them, and any UI showing battle numbers (skills page, skill tooltips) must consume the same functions rather than keeping a display-only copy — so a formula change propagates everywhere and stays under the parity suite's guard.

**Mid-battle attributes are a modifier graph, not a frozen snapshot.** `BattleAttributes` retains its modifier list instead of flattening to numbers at battle start, composing per-attribute totals through the same `computeAttributes` path the breakdown screen uses (memoised, recomputed only on add/remove). This mirrors the backend `AttributeCollection`, so both sides resolve derived attributes through one composition path even as values change mid-fight — the surface skill effects build on.

## Authentication

The frontend authenticates against the backend's JWT scheme (see `backend.md` for the server side). All token handling lives in the API client layer (`lib/api`) so the rest of the app never touches tokens:

- **`token-store.ts`** is the single source of truth for credentials — it persists the `{ accessToken, refreshToken }` pair in local storage (surviving a refresh) and decodes the access token's `exp` for pre-emptive refresh.
- **`auth.ts`** owns the refresh lifecycle and **collapses concurrent callers onto a single in-flight refresh** — refresh tokens are single-use on the backend, so parallel refreshes would consume the token twice and log the user out. It uses raw `fetch` to stay out of the 401-retry path and avoid an import cycle.
- **`api-request.ts`** attaches the bearer header, refreshes pre-emptively, and on a 401 refreshes once and retries. The anonymous auth endpoints are exempt.
- **`api-socket.ts`** passes the access token as a query param (browsers can't set WebSocket handshake headers), refreshes pre-emptively before opening the handshake (mirroring the HTTP path, so a reconnect on an expired token doesn't eat a rejected handshake), and on an auth-rejected handshake refreshes and reconnects (bounded — see _Socket connection lifecycle_). Because the open path is async, concurrent opens are collapsed onto a single in-flight connect.

**Role-based access checks.** The access token also carries the user's `role` claim(s); `hasRole(ERole.Admin)` is the single predicate the UI gates on (`ERole` mirrors the backend and is hand-maintained, not codegen'd). The sidebar hides role-gated screens and the `/admin` route self-guards on mount — but this is **UX only**; the backend independently enforces the Admin role on every admin endpoint. The generated API types (`lib/api/types`) come from the backend's codegen — regenerate them rather than hand-edit (see [infrastructure.md](./infrastructure.md#standalone-typescript-codegen)).

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

## Overlay systems (toast, modal, popover)

The global overlay UI shares one standardized design: a store/host pair in the root layout, presentation-only cards, and CSS-driven motion that collapses under `prefers-reduced-motion`. Toasts are transient notifications, modals are blocking confirm/acknowledge/destructive dialogs (a promise-based `await`-able API), and `Popover` is the non-blocking overlay. Prefer these over hand-rolling a backdrop. The per-system mechanics live in **[frontend-overlays.md](./frontend-overlays.md)**.

## Accessibility

Interactive controls are real elements, not `role="button"` divs — convert clickable `div`/`span`s to `<button>`. A few genuine exceptions remain where a native `<button>` doesn't fit (e.g. the SVG `<g>` allocation vertices in `attributes/AttributesRadar.svelte`).

**Tooltip association (`aria-describedby`).** A focusable trigger is linked to the explanation it surfaces through the global tooltip: each registered tooltip's container carries a stable id + `role="tooltip"` (`registerTooltipComponent` returns a `describedById`, forwarded on the shared controllers), and the `describedByTooltip` action (`components/tooltip/describedby-tooltip.ts`) wires it onto the trigger's `aria-describedby`. Opt in uniformly via that action rather than hand-rolling the id. Chosen over an `aria-live` region: it is the canonical tooltip pattern, ties the description to the trigger (recomputed on each focus), and avoids announcing on mouse hover. **It only helps triggers that surface the tooltip on keyboard focus** — purely hover-only surfaces (inventory item tiles, attribute chips, point-buy rows) must first be made focus-reachable before the action adds value.

**Accessible card pattern.** When a tile must host its own nested actions (favorite/unequip) and/or be a drag source, it can't itself be a `<button>` — nesting interactive elements is invalid HTML. Instead it uses a presentational container with a full-bleed primary-action `<button>` (the shared `components/OverlayButton.svelte`) stretched beneath the secondary action buttons, which sit at a higher `z-index` so they stay clickable. The overlay carries the drag handle and gets native keyboard activation — modifier state rides the synthesized click, so a ⌘/Ctrl-activate can branch (e.g. select vs. equip) without a manual `keydown`. A card with no primary action (an empty equip slot) renders no overlay, so it stays out of the tab order while remaining a drop target. Hover-only affordances on the presentational container (tooltips, drop highlighting) carry a `<!-- svelte-ignore a11y_no_static_element_interactions -->`.

## Styling

Most styling lives in each component's scoped scss; global styles and theming live in `UI/src/styles`. **All colours are CSS variables, used by semantic intent — never hard-coded.** Core colours are declared in `+layout.svelte` (pulled from `_colors.scss`); the palette is intentionally limited for consistency and contrast. The frontend is built for future custom themes that override those variables, so any colour must be a variable (added to `+layout.svelte`) and used according to its semantic meaning, even when two roles happen to share a hue.

A few cross-cutting conventions follow from that rule:

- **Data-driven accents go through helpers, not hex.** Rarity, challenge-type, item-category, and item-mod-type accents are all CSS variables read only via the `$lib/common` helpers (`rarityColor`/`rarityGlow`/`tintColor`/…) — never hard-code the hex, so a theme restyles every such surface by overriding the variables alone. `tintColor(color, alpha)` is the theme-overridable alternative to hard-coded rgba.
- **Change-state has its own token trio.** A pending add/edit/remove in the admin editor uses dedicated `--change-added`/`-modified`/`-removed` tokens, deliberately separate from `--accent`/`--warning`/`--enemy-accent` even where they currently share a hue — reserve `--warning` for validation.
- **Shared chrome is set once in `common.scss`** — the themed scrollbar (`--scrollbar-thumb`/`-hover`), an opaque `html, body` backstop behind the themeable `.page-base` surface (kills the first-frame white flash), and a `prefers-reduced-motion` collapse of transitions/animations. The reduced-motion rule doubles as the root-cause fix for motion-driven Playwright flakiness (the e2e config sets `reducedMotion: 'reduce'`) — prefer it over per-test waits.

## In-game screen design notes

Per-screen design decisions live in **[frontend-screens.md](./frontend-screens.md)**. When you add a new in-game screen, document its notable decisions there — surface a decision in this file only if it establishes a cross-cutting pattern other screens are expected to follow.

## Claude Design files

Most redesigned screens were crafted in the Claude Design tool; the chats and handoff artifacts are listed in **[designs.md](./designs/designs.md)** for context on the initial designs.
