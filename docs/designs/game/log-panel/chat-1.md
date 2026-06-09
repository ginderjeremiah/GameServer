# Log Panel (Initial Design)

_Extracted from the Login Form Modernization session, 2026-05-25 UTC. Covers the initial log panel layout variants. Continued in chat-2.md (Log Panel Design session, 2026-05-28)._

---

## User

Alright, next, let's get the log section fleshed out, since it is referenced in the fight screen. I'm already kinda partial to the idea of using the sliding manifest like in the loading screen, but I'd like you to come up with a few different approaches for the overall structure and how each log message is styled/displayed.

## Assistant

_[tool: write_file]_

Plan: 4 directions for the log, all auto-feeding so you can see them live. I'll keep each focused on the log panel against a dark backdrop (not in the full game frame) so it's easy to compare the structural differences.

- **A · Sliding Manifest** — windowed list that cycles upward, same vocabulary as the loading screen
- **B · Streaming Feed** — traditional reverse-chronological feed, newest at top, color-coded with icons
- **C · Event Cards** — each entry as a horizontal card with icon, actor verb, and big numeric on the right
- **D · Latest + History** — large callout for the most recent event with a condensed history below

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: done]_

_[tool: fork_verifier_agent]_

Four log directions on the canvas, all sharing the same auto-feed (new entry every ~1.1s):

- **A · Sliding Manifest** — windowed list cycling upward, mono throughout, edges fade. Closest sibling to the loading screen — quiet and dense.
- **B · Streaming Feed** — chronological list with `[time] [glyph] [colored message with actor/skill/target highlighted] [numeric pill]`. Crits get a small `CRIT` tag.
- **C · Event Cards** — each entry as a left-accent-stripe card: glyph box → actor·skill→target line + meta subline → big damage number on the right. ESPN-ticker feel.
- **D · Latest + History** — top spotlight card with the most recent event large (kind label, message, big damage number), brief flash on each new entry; four older entries compact below.

Shared color key across all four:
- Player action = blue · Enemy action = pale red · Loot = green · Crit = gold · System = gray

Things to decide before wiring this in:
1. Which one (or hybrid)
2. Newest-at-top vs newest-at-bottom for B (currently bottom, combat-log convention)
3. For D — should the spotlight crossfade or pop-in?
4. Crits — currently flagged with a `CRIT` tag in B + bigger number in C/D. Want them louder? (gold flash, screen-shake feel)

## User

Verifier agent check completed

## Assistant

Verifier passed. Standing by for your pick.
