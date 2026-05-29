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
- When writing logic or classes for the frontend, make use of the [statify](..\UI\new-svelte\src\lib\common\statify.svelte.ts) to make a class reactive in Svelte. This will allow you to write more object-oriented code and keep your components cleaner, while still maintaining reactivity in the UI. This is especially useful for UI related to battle simulation, where there is a lot of complex logic and state that would be cumbersome to manage directly in Svelte stores or components.
- Make sure tests go in a corresponding folder in the `tests` folder with the same structure as what you are testing in the `src` folder. Do NOT put tests in the same folder as the code they are testing.

# Important Design Decisions

## Reference Data

Most of the reference data in the frontend is fetched from the backend API on application startup and cached in-memory in Svelte stores for fast access. This includes things like items, item modifications, skills, enemies, zones, and challenges. Since this data is static and does not change often, it is not a problem to cache it in-memory, and it allows for much faster access than fetching from the API every time it is needed. Eventually this data will be cached in persistent browser storage and only fetched from the API when it changes, but for now it is simply fetched on startup for simplicity.
