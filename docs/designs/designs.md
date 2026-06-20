# Design Handoff Bundles

VERY IMPORTANT!!! - These are NOT representative of what is currently in the game. They are here for historical purposes and only used for the initial implementation of the designs. In many cases that actual game design has evolved beyond what is here and these should NOT be used as a reference for the game's current UI.

These folders contain Claude Design handoff bundles — HTML/CSS/JS prototypes exported from claude.ai/design for implementation in the game's Svelte frontend, organized by screen under two top-level groups:

- [`admin/`](./admin/) — internal tooling screens
- [`game/`](./game/) — player-facing screens

Each screen folder contains the HTML entry-point prototype(s), the JSX component/data files they reference, and a `chat.md` (or `chat-*.md`) with the design conversation that produced them. A [`game/shared/`](./game/shared/) folder holds cross-screen prototype utilities (`game-shared.jsx`, `design-canvas.jsx`, `variant-*`, `tweaks-panel.jsx`).

> **Note on prototype imports:** the HTML files were originally all in the same flat directory and reference sibling JSX files by relative path. After reorganization those relative imports are broken, but the component source is still fully readable as reference — a coding agent should read the HTML and JSX files directly rather than trying to run them in a browser.

---

## Admin

| Folder                                      | Screens                                      | Chat      |
| ------------------------------------------- | -------------------------------------------- | --------- |
| [admin-tools](./admin/admin-tools/)         | Admin Tools (sidebar layout, nav redesign)   | `chat.md` |
| [enemy-admin](./admin/enemy-admin/)         | Enemy Admin Workbench, Workbench Interactive | `chat.md` |
| [challenge-admin](./admin/challenge-admin/) | Challenge Admin editor                       | `chat.md` |

---

## Game

| Folder                             | Screens                                                    | Chat                                                                                                                     |
| ---------------------------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| [login](./game/login/)             | Login / account-creation form                              | `chat.md`                                                                                                                |
| [loading](./game/loading/)         | Loading screen                                             | `chat.md`                                                                                                                |
| [game-screen](./game/game-screen/) | Main game layout (collapsible sidebar nav)                 | `chat.md`                                                                                                                |
| [fight](./game/fight/)             | Fight screen layout, combat-feedback enhancements          | `chat-original.md` (initial), `chat.md` (boss redesign), `chat-enhancements.md` (XP bar, floating numbers, battle timer) |
| [inventory](./game/inventory/)     | Inventory screen (drag-drop, item mods)                    | `chat.md`                                                                                                                |
| [challenge](./game/challenge/)     | Challenge screen v1/v2/v3 + redesign                       | `chat.md` (original), `chat-redesign.md`                                                                                 |
| [attributes](./game/attributes/)   | Attribute page, breakdown, build, stats-attributes sidebar | — (see stats/chat.md)                                                                                                    |
| [stats](./game/stats/)             | Statistics page and sidebar panels                         | `chat.md` (also covers attributes)                                                                                       |
| [skills](./game/skills/)           | Skills Inspector wireframes and themed pass                | `chat.md`                                                                                                                |
| [options](./game/options/)         | Options / settings screen                                  | `chat.md`                                                                                                                |
| [log-panel](./game/log-panel/)     | Battle log panel                                           | `chat-1.md` (initial), `chat-2.md` (refinement)                                                                          |
| [toast-modal](./game/toast-modal/) | Toast & modal notification system                          | `chat.md`                                                                                                                |
| [tooltips](./game/tooltips/)       | Tooltip components                                         | `chat.md`                                                                                                                |
| [card-game](./game/card-game/)     | Card minigame (Loom) wireframes and themed passes          | `chat.md`                                                                                                                |
| [shared](./game/shared/)           | Cross-screen prototype utilities (not screen-specific)     | —                                                                                                                        |
| [codex](./game/codex)              | Codex page and relevant tab designs                        | —                                                                                                                        |
