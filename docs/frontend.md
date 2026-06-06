# File Structure

All of the frontend code is contained in the `UI` folder, and all the new code is in the `new-svelte` folder within that. The rest of the code in the `UI` folder is the old vanilla Typescript code that will be kept around until the new Svelte frontend reaches feature parity with the old frontend, at which point the old code will be removed. The `new-svelte` folder is structured as follows:

- `src`: All the Svelte components, stores, and related code for the new frontend.
  - `components`: Various shared Svelte components used in the frontend.
  - `lib`: API client code, utility functions, and battle simulation logic.
    - `api`: API client code (HTTP and WebSockets) and auto-generated API client interfaces.
    - `battle`: Battle simulation domain objects
    - `common`: Utility functions/classes used across the frontend
    - `engine`: Core game engine logic - render/logical engine implementations, battle engine, player/enemy managers, etc.
  - `routes`: Svelte components for each route/page in the application, also includes any route-specific components
  - `stores`: Svelte stores for managing application state, such as log data, static reference data, and tooltip data. NOTE: most stateful data is instead being moved to classes in the `lib` folder that are made reactive using the `statify` utility function, so avoid adding new stores if possible and consider whether the state you need can be managed using a reactive class.
  - `styles`: Global styles and theming for the frontend.inventory page, etc.
  - `tests`: Unit tests for the lib code, pages, and components
- `static`: Static assets such as images, icons, and fonts used in the frontend.
- `e2e-tests`: Playwright end-to-end tests for the frontend components

# General Frontend Guidelines

In addition to the ongoing goal of achieving feature parity with the old frontend, there is also work being done to redesign the UI/UX of the frontend to be more user-friendly and visually appealing. Keep in mind that not all UI/UX decisions in the new frontend are final.

- When creating new components avoid putting too much markup in a single component. If a component is getting too large or complex or you feel the need to write comments indicating each section, break it down into smaller subcomponents and put them in their own folder if there are several. Even if they are only used by the parent component, it is still an improvement to readability and maintainability.
- When writing logic or classes for the frontend, make use of [statify](..\UI\new-svelte\src\lib\common\statify.svelte.ts) to make a class reactive in Svelte. This will allow you to write more object-oriented code and keep your components cleaner, while still maintaining reactivity in the UI. This is especially useful for UI related to battle simulation, where there is a lot of complex logic and state that would be cumbersome to manage directly in Svelte stores or components.

## Testing Guidelines

Unit tests should be written for all pages, components, and lib code in the new frontend. The tests should be placed in the `tests` folder with a corresponding folder structure to the code being tested. For example, if you are writing a test for a component in `src/components/MyComponent.svelte`, the test should be placed in `tests/components/MyComponent.test.ts`. End-to-end tests should be written using Playwright and placed in the `e2e-tests` folder. You should only write end-to-end tests for critical user flows and features rather than trying to cover every possible interaction in the UI. End-to-end tests are more time-consuming to write and run, so they should be reserved for testing the most important aspects of the user experience and should be written from the perspective of how a user would interact with the application.

# Important Architectural Design Decisions

## Authentication (JWT bearer + rotating refresh tokens)

The frontend authenticates against the backend's JWT scheme (see the backend doc for the server side). All of the token handling lives in the API client layer (`lib/api`) so the rest of the app never touches tokens directly:

- **`token-store.ts`** is the single source of truth for credentials: it persists the `{ accessToken, refreshToken }` pair in local storage (so a logged-in session survives a page refresh — the "stay logged in" behaviour) and can decode the access token's `exp` claim for pre-emptive refresh.
- **`auth.ts`** owns the refresh lifecycle. `refreshTokens()` exchanges the stored refresh token for a new pair and **collapses concurrent callers onto a single in-flight request** — refresh tokens are single-use on the backend, so parallel refreshes would consume the token twice and log the user out. It deliberately uses `fetch` (not `ApiRequest`) to stay out of the 401-retry path and avoid an import cycle. `ensureValidAccessToken()` refreshes just before expiry; `handleAuthFailure()` clears storage and routes to login when refresh is no longer possible.
- **`api-request.ts`** attaches `Authorization: Bearer <accessToken>` to every authenticated request, refreshes pre-emptively before sending, and on a `401` refreshes once and retries. The `[AllowAnonymous]` auth endpoints (`Login`, `Login/CreateAccount`, `Login/Refresh`, `Login/Logout`) are exempt from this machinery.
- **`api-socket.ts`** passes the access token as the `?access_token=` query parameter (browsers can't set headers on the WS handshake) and, if a handshake is rejected for auth reasons (the socket closes before it ever opens), refreshes once and reconnects.

Login stores the pair from the `LoginResult` and enters the game; logout sends the refresh token so the backend can revoke it, then clears storage. The generated API types (`lib/api/types`) are produced by the backend's codegen — when changing a DTO, regenerate them rather than editing the files by hand, either by running the backend in Development or, without standing up the API/database, via `dotnet run --project Game.Api -- codegen` (see [backend.md](backend.md#standalone-typescript-codegen)).

## Reference Data

Most of the reference data in the frontend is fetched from the backend API on application startup and cached in-memory in Svelte stores for fast access. This includes things like items, item modifications, skills, enemies, zones, and challenges. Since this data is static and does not change often, it is not a problem to cache it in-memory, and it allows for much faster access than fetching from the API every time it is needed. Eventually this data will be cached in persistent browser storage and only fetched from the API when it changes, but for now it is simply fetched on startup for simplicity.

## Admin Tools: the entity-driven Workbench

The admin tools (`src/routes/admin`) were originally a separate page per operation — e.g. for enemies alone you had to visit _Add/Edit Enemies_, _Set Attribute Distributions_, _Set Enemy Skills_, and _Set Zone Enemies_ (under Zones), re-selecting the enemy in each. This was consolidated into a single, config-driven **Workbench** (`src/routes/admin/workbench`): a master–detail page with a searchable record list on the left and a tabbed detail panel on the right (Identity + the related collections that used to be their own tools), with one catalogue-wide "Save Changes" bar.

Key decisions and rationale:

- **One component, many entities.** A single `Workbench.svelte` renders any entity described by an `EntityConfig` (`workbench/entities/*.ts`). A config declares the entity's list metadata and an ordered list of **sections**, each of kind `fields` | `table` | `chips` | `tags` | `usage` | `challenge-condition` | `challenge-reward`. Adding a new admin entity is a new config object, not new UI. The seven current entities (Enemies, Skills, Items, Item Mods, Zones, Tags, Challenges) are grouped in the sidebar as Combat / Items / World / Progression.
- **Custom section kinds stay config-driven.** When a section needs conditional logic the generic kinds don't cover, it gets its own `kind` (rendered by `SectionRenderer`) rather than forking the Workbench. Two opt-in `EntityConfig`/section hooks keep this generic: `headline(record)` renders a one-line preview under the detail title, and a custom section declares `dirtyKeys` (the record fields that constitute a change) so its tab still shows the unsaved dot. Existing entities set neither and are unaffected.
- **Diffing store.** `EntityStore` (`entity-store.svelte.ts`) keeps an editable copy of the records plus a baseline and derives per-record status (`added` / `modified` / `deleted` / `clean`) by structural comparison. The save bar shows live added/edited/removed counts; Discard reverts to the baseline; per-record Reset/Delete/Restore are in the detail header. This is the same dirty-tracking model as the generic `TableEditor`, generalized to whole records.
- **Validation lives where it occurs.** A field is `required` (with its own message) or a non-field section carries a `warn(record)` predicate (`validation.ts`). The record's list row, the offending tab, and the field all surface the same warning. The amber **dot** is reserved for unsaved/dirty state; the amber **triangle** means a validation warning — the two signals never overlap.
- **Save orchestration.** "Save all" commits every pending change across the catalogue. Because the backend persists each relationship through its own endpoint, `persistEntity` (`save-helpers.ts`) first posts the identity-level Add/Edit/Delete batch, refetches to resolve the ids of newly-added records (the backend appends adds in send order, so the k-th add maps to the k-th new id), then runs each section's child-saver against the resolved id. This keeps the single-save UX while reusing the existing per-relationship endpoints.
- **Each save only calls the endpoints it needs:** a record is `modified` whenever any part of it (identity _or_ a child collection) differs from baseline, so `persistEntity` sends an identity-level Edit only when the primary DTO actually changed, and each child-saver no-ops when its own collection is unchanged. Editing just a skill's damage multipliers therefore hits only `SetSkillMultipliers`, not `AddEditSkills`, and vice-versa.

## Toast notifications

User-facing notifications (currently errors, but the system is type-aware for future success/warning/info use) are handled by a single global toast system. The `toast` store (`src/stores/toast.svelte.ts`) owns the active toasts in a `SvelteMap` keyed by a stable id — mirroring the tooltip store, so dismissal is reference- and order-independent even while the container is rendering the collection — and exposes `showToast` plus the `toastError` / `toastSuccess` / `toastWarning` / `toastInfo` helpers. A single `ToastContainer` mounted in the root `+layout.svelte` renders the stack; individual `Toast` components are presentation-only and accent themselves by type via the existing `--error` / `--success` / `--warning` / `--accent` CSS variables.

Key decisions and rationale:

- **The store is the single entry point.** Anywhere in the app can surface a notification by importing a helper from `$stores` and calling it — there is no need to thread props or context through components. Error sources are wired to the store rather than rendering their own ad-hoc UI: socket failures (`onSocketError`) are subscribed once in the root layout, and the admin workbench reports save (`EntityStore.save`) and reference-load failures that were previously swallowed.
- **Duplicate messages collapse.** Repeated identical messages of the same type (e.g. a socket error that keeps firing) refresh the existing toast's auto-dismiss timer instead of stacking, so a flapping connection can't bury the screen.
- **Auto-dismiss is opt-out.** Toasts auto-dismiss after a default duration; pass `duration: 0` for a sticky toast that only a manual dismiss (or `clearToasts`) removes. The timer handles live in a deliberately non-reactive `Map` since nothing renders from them.

## Styling

A lot of styling is done in the encapsulated scss style blocks of each Svelte component, but there are also some global styles and theming defined in `UI/src/styles`. The core default colors used in the frontend should be defined as CSS variables in `UI/src/routes/+layout.svelte` (and pulled from `UI/src/styles/_colors.scss`) and used throughout the application. The color palette is intentionally limited to a few core colors to maintain a consistent visual theme and make it easier to ensure good contrast and accessibility. That being said, the frontend was designed with the intention of allowing custom themes to be added in the future which will override the CSS variables defined in `+layout.svelte`. Any colors used in the frontend should be added as a CSS variable in `+layout.svelte` and not hardcoded in the styles of individual components, to ensure that they can be easily overridden by custom themes in the future.

### Reduced motion

`styles/common.scss` honours the OS `prefers-reduced-motion: reduce` preference by collapsing all transitions/animations to a near-instant `0.01ms` (kept non-zero so any `transitionend`/`animationend` listeners still fire). Beyond the accessibility win, this is the root-cause fix for a class of Playwright flakiness: the e2e config sets `use.reducedMotion: 'reduce'`, so motion-driven layout shifts (e.g. the hover-expand admin sidebar moving buttons mid-click) settle instantly and clicks stop being dropped and retried until the 30s test timeout. Prefer this over per-test waits when a UI animation makes an element unstable under test.

### Rarity palette

Rarity visuals (used by both the in-game inventory and the admin workbench) follow the rule above and have a single source of truth: the per-tier hue (`--rarity-common` … `--rarity-mythic`) and glow intensity (`--rarity-*-glow`, a unitless `0–1` value) are declared as CSS variables in `+layout.svelte`. The helpers in `$lib/common/rarity` are the only place that reads them — `rarityColor`/`rarityGlow` return the `var(...)` reference, `rarityLabel`/`rarityLevel` derive their values from the `ERarity` enum (the enum value doubles as the tier), and `rarityTint(id, alpha)` produces a `color-mix(in srgb, …)` so a rarity hue can be drawn at partial opacity while still flowing through theme overrides. Components must use these helpers rather than hard-coding rarity hex values or computing rgba blends, so a theme can restyle every rarity surface (grid glow, drawer/tooltip tags, mod names, admin badges) by overriding the variables alone.

### Themeable accent palettes & `tintColor`

The rarity pattern is now the template for every data-driven accent. Challenge-type accents (`--challenge-*`, one per `EChallengeType`), item-category accents (`--category-armor`/`-weapon`/`-accessory`) and item-mod-type accents (`--mod-component`/`-prefix`/`-suffix`) are all declared as CSS variables in `+layout.svelte` and read only through helpers in `$lib/common` (`challengeTypeColor`/`challengeTypeTint`, `itemCategoryColor`/`itemCategoryName`, `modTypeColor`/`modTypeLabel`). The generic `tintColor(color, alpha)` helper produces a `color-mix(in srgb, …)` for **any** colour (hex, named, or a `var(--x)`), so partial-opacity blends stay theme-overridable; `rarityTint` is now a thin wrapper over it. Prefer `var(--…)` + `tintColor` over hard-coded hex/rgba.

### Tooltip surface & item-tooltip accents

The global tooltip container (`components/Tooltip.svelte`, shared by every tooltip) paints its panel with the themeable `--tooltip-bg` var — a `color-mix` over `--surface` whose tint percentage is the single, theme-overridable see-through knob (paired with a backdrop blur for legibility; the translucency lives in the background, not an element `opacity`, so text/accents stay crisp). Item/mod tooltips set no background of their own and inherit it.

For the inventory **item tooltip** (`ItemTooltip.svelte`) the convention mirrors `ModTooltip`: the panel's **left-border accent reflects the item's rarity** (`rarityColor(item.rarityId)`), while the category row (diamond + label) stays **category-coloured**, and each filled mod tile is accented by **that mod's** rarity. The displayed item name is composed from its applied mods via `composeItemName(baseName, mods)` (prefix mod names prepend, suffix names append, components are name-neutral), and the Stats section reflects the merged item + applied-mod attributes (`Item.totalAttributes`).

### Editor change-state accents

The admin editor (the shared `components/table` `TableEditor` and the `routes/admin/workbench`) signals a record/row/cell that is a pending **add**, **edit**, or **remove** with a dedicated token trio — `--change-added` / `--change-modified` / `--change-removed` — declared in `+layout.svelte`. These currently resolve to the same hues as `--accent` / `--warning` / `--enemy-accent`, but they are deliberately separate functional concerns: a row marked for removal is not an "enemy", and an unsaved/dirty edit is not a validation "warning". Keeping them distinct means a theme can restyle change-state affordances (status edges, the add/edit/remove pips and spill tags, the dirty-field/tab dots, the delete/reset action hovers) without disturbing the brand accent, the destructive-enemy hue, or the amber validation-warning signal — which the Workbench intentionally keeps visually separate from the unsaved-dirty signal. Use these tokens for change-state UI and reserve `--warning` for validation.

## Challenge screen (the Type Rail)

The in-game Challenges screen (`src/routes/game/screens/challenges`) is a **type-driven master/detail** view: a left rail lists each `ChallengeType` (ring meter + done/total) plus an "Overview" entry; selecting a type swaps the right pane to a hero banner + that type's challenge cards with an in-page Progress/Rarity/Name sort, while "Overview" shows overall progress, a pulsing "next up" reward, and a per-type grid. It reuses the existing screen chrome (dark frame, diamond mark, Geist/Geist Mono) and is decomposed into many small components per the component-size guideline.

Key decisions and rationale:

- **Rewards are sealed until unlocked.** Every item/mod is unlocked by completing a challenge (random drops were scrapped — see the game-design doc), so a locked reward is shown as a _sealed_ teaser: its rarity tier + category are hinted but the name is `???`. On completion it _reveals_ — the name appears in rarity colour and hovering it opens the same tooltip you'd see inspecting it in your inventory. Reward resolution, reveal-gating and progress maths live in the pure helpers in `challenges-view.svelte.ts` (heavily unit-tested); the reactive `ChallengesView` class only wires state to `$derived`, mirroring `InventoryView`.
- **One tooltip, driven by context.** Rather than re-implementing the design mock's bespoke anchored tooltip, the screen reuses the **global tooltip system** (`registerTooltipComponent`). A single `RewardTooltip` dispatcher renders the right variant — the real `ItemTooltip.svelte` for revealed items (single source of truth), a new `ModTooltip` for revealed mods, and masked `SealedItem`/`SealedModTooltip` for sealed rewards (name/values redacted, but the _count_ of stat rows and mod slots stays truthful). The hover controller is provided through Svelte **context** (`reward-tooltip-context.ts`) so deeply-nested reward affordances don't have to thread handlers down the tree.

## Options screen (the settings shell)

The in-game Options screen (`src/routes/game/screens/options`) reimagines the old logging pop-up as an **extensible two-pane settings shell** living inside the existing game frame: a category rail (left) + the active category's content + a Save/Discard footer. It reuses the established chrome (dark frame, diamond mark, Geist/Geist Mono) and is decomposed into small components per the component-size guideline.

Key decisions and rationale:

- **Config-driven and extensible.** The category list (`SETTINGS_CATS`), log groups (`LOG_GROUPS`) and per-type metadata (`LOG_TYPES`, which mirrors `ELogType`) are plain arrays in `options-view.svelte.ts`. Adding a future settings category or a new log type is a one-line edit, not new UI. Only **Logging** is built; Display/Audio/Gameplay/Account render as disabled `soon` rail items to demonstrate the shell extends cleanly.
- **Diff-only save, mirroring the old `LogManager`.** `OptionsView` keeps a `draft` working copy against a persisted `baseline` and derives the dirty set by comparison (the same model as the inventory/challenge view-models). Save sends only the changed `ILogPreference`s to `Player/SaveLogPreferences`. The diff and the changed-set are computed from plain `$state` (not the `$derived` getters) so they are safe to read from non-reactive call sites like `save()`.
- **Optimistic local apply.** The combat-log writer (`log.ts`) filters by `playerManager.logPreferences`, so saving pushes the draft onto the player manager immediately — the change takes effect for the session regardless of the network result. The server endpoint is not yet implemented, so a failed persist surfaces a toast warning (the change still applies locally) rather than blocking the UI. When the backend lands this becomes a normal success path with no UI change.
- **Live preview reuses the real log row.** The right-hand preview renders a self-contained sample event stream through the actual `LogRow` component, filtered by the _draft_ preferences — so it looks exactly like the real combat log and visibly reflects each toggle before you save.
- **All colour flows from the root vars.** The semantic combat-log palette lives in the root `--log-*` CSS variables (`+layout.svelte`); `logColors` in `log-kind.ts` is just the `var(--log-*)` reference map shared by the real log, the preview and the per-type rows. Every neutral/alpha shade in the screen is `color-mix(in srgb, var(--…) N%, transparent)` over a base token (off-white `--text-primary`, `--white`, `--black`, the `--eyebrow` accent) or an existing semantic var (`--text-secondary/-tertiary/-muted`, `--border-subtle/-light/-medium`) — no hard-coded `rgba(…)`, so a theme can restyle the whole screen by overriding the variables alone. Follow-up: the shared chrome (`components/table` editor, `nav-menu`/`admin` sidebars) and `components/log-panel` now follow this same pattern; the remaining hard-coded neutrals live in the inventory and loading game screens, which are tracked separately.

## Loading screen (the boot manifest)

The loading screen (`src/routes/loading`) runs the boot sequence that fetches the static reference data the game needs and surfaces it as a sequential "manifest" the player can watch tick through. Like the Options/Challenge screens, it is decomposed into small components per the component-size guideline rather than living as one large `+page.svelte`.

Key decisions and rationale:

- **Load orchestration is a reactive view-model.** `LoadingView` (`loading-view.svelte.ts`) owns the manifest state machine (`phase`, `items`, `activeIndex`, the `completed`/`progressPct`/`title` deriveds) and the `start`/`retryFailed`/`enterGame` actions — mirroring `OptionsView`/`InventoryView`. `+page.svelte` just constructs it and renders the presentational subcomponents (`DiamondMark`, `LoadingHeader`, `ProgressBar`, `ManifestWindow` + `ManifestRow`, `ErrorBanner`, `EnterButton`). This makes the load logic unit-testable without rendering.
- **The reference-data sets are a single table.** Each set is one row in the `REFERENCE_DATA` array (`key` + `label` + a `loaded()` cache check + a typed `load()`), so adding/removing a set is a one-line edit rather than a copy-pasted block. Concurrent fetches of the same key are de-duplicated through a process-wide in-flight cache so a re-mount mid-load can't double-fetch.
- **The manifest window keeps its own DOM state.** The centred sliding window — the wheel handler, the `requestAnimationFrame` easing toward the active row, and the per-row opacity falloff — lives inside `ManifestWindow`, driven only by the `items`/`activeIndex`/`phase` props, keeping that view concern out of the load model.
- **All colour flows from the root vars.** The semantic combat-log palette lives in the root `--log-*` CSS variables (`+layout.svelte`); `logColors` in `log-kind.ts` is just the `var(--log-*)` reference map shared by the real log, the preview and the per-type rows. Every neutral/alpha shade in the screen is `color-mix(in srgb, var(--…) N%, transparent)` over a base token (off-white `--text-primary`, `--white`, `--black`, the `--eyebrow` accent) or an existing semantic var (`--text-secondary/-tertiary/-muted`, `--border-subtle/-light/-medium`) — no hard-coded `rgba(…)`, so a theme can restyle the whole screen by overriding the variables alone. Follow-up: a few other game screens (e.g. the loading screen) still carry pre-existing hard-coded neutrals that should migrate to this same pattern.
