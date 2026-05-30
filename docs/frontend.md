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
  - `tests`: Unit tests for the lib code and components
- `static`: Static assets such as images, icons, and fonts used in the frontend.
- `tests`: Playwright integration tests for the frontend components

# General Frontend Guidelines

In addition to the ongoing goal of achieving feature parity with the old frontend, there is also work being done to redesign the UI/UX of the frontend to be more user-friendly and visually appealing. Keep in mind that not all UI/UX decisions in the new frontend are final.

- When creating new components avoid putting too much markup in a single component. If a component is getting too large or complex or you feel the need to write comments indicating each section, break it down into smaller subcomponents and put them in their own folder if there are several. Even if they are only used by the parent component, it is still an improvement to readability and maintainability.
- When writing logic or classes for the frontend, make use of [statify](..\UI\new-svelte\src\lib\common\statify.svelte.ts) to make a class reactive in Svelte. This will allow you to write more object-oriented code and keep your components cleaner, while still maintaining reactivity in the UI. This is especially useful for UI related to battle simulation, where there is a lot of complex logic and state that would be cumbersome to manage directly in Svelte stores or components.
- Make sure tests go in a corresponding folder in the `tests` folder with the same structure as what you are testing in the `src` folder. Do NOT put tests in the same folder as the code they are testing.

# Important Design Decisions

## Reference Data

Most of the reference data in the frontend is fetched from the backend API on application startup and cached in-memory in Svelte stores for fast access. This includes things like items, item modifications, skills, enemies, zones, and challenges. Since this data is static and does not change often, it is not a problem to cache it in-memory, and it allows for much faster access than fetching from the API every time it is needed. Eventually this data will be cached in persistent browser storage and only fetched from the API when it changes, but for now it is simply fetched on startup for simplicity.

## Admin Tools Shell

The admin tools page uses the same collapsible left rail as the main game (`routes/admin/AdminSidebar.svelte`) instead of the legacy horizontal `NavMenu`. Entity types (Enemies / Items / Skills / Zones) are sidebar groups and each tool is an item; a "Return to Game" item and a status footer sit at the bottom. The generic `TableEditor`/`TableRow`/`TableCell` were reworked into the dark design system:

- **Row/cell state is derived, not stored.** Each row compares its cells to a saved `originalData` snapshot, so undo-after-delete returns a row to its correct modified/clean state and reverting every cell returns the row (and the footer change count) to clean. Changed cells show a warning-tinted border + amber dot; reverting clears them.
- **Per-row actions:** `Delete` (existing rows render dimmed with a `Restore`; added rows just disappear) and `Reset` (only on modified rows) which reverts to the original values.
- **Number fields reject non-numeric input** at the keystroke (digits, an optional leading minus, a single decimal point).
- A **footer save bar** shows the pending-change summary (added / edited / removed pips) with Discard + Save Changes; `onSave` is supplied per tool. Column editors are still inferred from each value's type and the tool's `sampleItem`, so tools never hardcode columns — they only declare data, `selectOptions`, hidden columns, and `onSave`.

## Combat Log — Sliding Manifest

`components/log-panel/` renders the log as a windowed "sliding manifest": newest entry on top, a kind chip + glyph + color derived from `ELogType` (`log-kind.ts`), a smooth slide/grow entrance on the newest row (keyed by the newest id so it only plays for genuinely new entries), a bottom-only edge fade, and manual scrolling that preserves the user's position when they've scrolled into history. The log model is a flat `{ id, logType, message }`, so player-vs-enemy `Damage` entries are distinguished by the message prefix the battle engine emits ("You used …"); no structured numeric callout is fabricated.

## Inventory — Loadout Rail

`routes/game/screens/Inventory/` is a three-column "Loadout Rail": a labeled equipped rail (left), the item grid (center), and a dedicated **Equipped Totals** panel (right) that sums every attribute across equipped items and their applied mods. Clicking an item opens a right-side slide-over **detail/mod drawer** (dimmed backdrop, click-outside to close). UI state lives in a reactive view-model (`InventoryView` in `inventory-view.svelte.ts`) seeded from `inventoryManager`; actions update the local copies immediately and delegate persistence to the manager.

- **Item rarity** drives the grid-cell border color (white → green → blue → purple → gold → red) and glow intensity; the category is shown as a small neutral corner glyph (the center is a stand-in until real item art lands). Rarity (`ERarity`) is now part of the `IItem` model.
- **Equipping** is kept as drag-to-slot and also supports ⌘/Ctrl-click and an Equip button (low-click, leaving room for the planned loadouts). Slots are data-driven from `EQUIP_SLOTS`, so adding/splitting equipment slots is a one-line change.
- **Favoriting** is a star toggle (optimistic local update + `SetItemFavorite` websocket command); items can be filtered to favorites and sorted by Name / Category.
- **Mods:** the finalized item tooltip (reused on hover) and the drawer show every mod slot — filled *and empty* — and the drawer installs/removes compatible unlocked mods.
