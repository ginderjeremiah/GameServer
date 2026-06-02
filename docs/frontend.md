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

# Important Architectural Design Decisions

## Reference Data

Most of the reference data in the frontend is fetched from the backend API on application startup and cached in-memory in Svelte stores for fast access. This includes things like items, item modifications, skills, enemies, zones, and challenges. Since this data is static and does not change often, it is not a problem to cache it in-memory, and it allows for much faster access than fetching from the API every time it is needed. Eventually this data will be cached in persistent browser storage and only fetched from the API when it changes, but for now it is simply fetched on startup for simplicity.

## Admin Tools: the entity-driven Workbench

The admin tools (`src/routes/admin`) were originally a separate page per operation — e.g. for enemies alone you had to visit _Add/Edit Enemies_, _Set Attribute Distributions_, _Set Enemy Skills_, and _Set Zone Enemies_ (under Zones), re-selecting the enemy in each. This was consolidated into a single, config-driven **Workbench** (`src/routes/admin/workbench`): a master–detail page with a searchable record list on the left and a tabbed detail panel on the right (Identity + the related collections that used to be their own tools), with one catalogue-wide "Save Changes" bar.

Key decisions and rationale:

- **One component, many entities.** A single `Workbench.svelte` renders any entity described by an `EntityConfig` (`workbench/entities/*.ts`). A config declares the entity's list metadata and an ordered list of **sections**, each of kind `fields` | `table` | `chips` | `tags` | `usage`. Adding a new admin entity is a new config object, not new UI. The six current entities (Enemies, Skills, Items, Item Mods, Zones, Tags) are grouped in the sidebar as Combat / Items / World.
- **Diffing store.** `EntityStore` (`entity-store.svelte.ts`) keeps an editable copy of the records plus a baseline and derives per-record status (`added` / `modified` / `deleted` / `clean`) by structural comparison. The save bar shows live added/edited/removed counts; Discard reverts to the baseline; per-record Reset/Delete/Restore are in the detail header. This is the same dirty-tracking model as the generic `TableEditor`, generalized to whole records.
- **Validation lives where it occurs.** A field is `required` (with its own message) or a non-field section carries a `warn(record)` predicate (`validation.ts`). The record's list row, the offending tab, and the field all surface the same warning. The amber **dot** is reserved for unsaved/dirty state; the amber **triangle** means a validation warning — the two signals never overlap.
- **Save orchestration.** "Save all" commits every pending change across the catalogue. Because the backend persists each relationship through its own endpoint, `persistEntity` (`save-helpers.ts`) first posts the identity-level Add/Edit/Delete batch, refetches to resolve the ids of newly-added records (the backend appends adds in send order, so the k-th add maps to the k-th new id), then runs each section's child-saver against the resolved id. This keeps the single-save UX while reusing the existing per-relationship endpoints.
- **Each save only calls the endpoints it needs:** a record is `modified` whenever any part of it (identity _or_ a child collection) differs from baseline, so `persistEntity` sends an identity-level Edit only when the primary DTO actually changed, and each child-saver no-ops when its own collection is unchanged. Editing just a skill's damage multipliers therefore hits only `SetSkillMultipliers`, not `AddEditSkills`, and vice-versa.

## Styling

A lot of styling is done in the encapsulated scss style blocks of each Svelte component, but there are also some global styles and theming defined in `UI/src/styles`. The core default colors used in the frontend should be defined as CSS variables in `UI/src/routes/+layout.svelte` (and pulled from `UI/src/styles/_colors.scss`) and used throughout the application. The color palette is intentionally limited to a few core colors to maintain a consistent visual theme and make it easier to ensure good contrast and accessibility. That being said, the frontend was designed with the intention of allowing custom themes to be added in the future which will override the CSS variables defined in `+layout.svelte`. Any colors used in the frontend should be added as a CSS variable in `+layout.svelte` and not hardcoded in the styles of individual components, to ensure that they can be easily overridden by custom themes in the future.
