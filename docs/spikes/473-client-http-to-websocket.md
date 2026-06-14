# Spike #473 — Remaining client HTTP usage and the plan to move it to WebSockets

- **Spike issue:** [#473](https://github.com/ginderjeremiah/GameServer/issues/473)
- **Status:** Research complete; direction reviewed and approved by the project owner (spike PR #562 merged); split into implementation sub-issues of #473 (see [Implementation issues](#implementation-issues)).

## Goal

`docs/backend.md` → _HTTP vs WebSocket Communication_ states the direction: move all
frontend↔backend communication onto the socket **except login/account creation**, since a
single transport is what eventually lets the backend keep player state in-memory. This spike
inventories every HTTP call the client still makes, decides which should move, and weighs the
owner's secondary ask — front-loading the challenges/statistics data and updating it
optimistically in the client.

## Current client HTTP surface

The full set of HTTP endpoints (`UI/src/lib/api/types/api-type-map.ts`) and their disposition:

| Endpoint | Returns | Caller (prod) | Disposition |
|---|---|---|---|
| `Login` | `ILoginResult` | `routes/+page.svelte` | **Stays HTTP** — login |
| `Login/CreateAccount` | — | `routes/+page.svelte` | **Stays HTTP** — account creation |
| `Login/Refresh` | `IAuthTokens` | `lib/api/auth.ts` | **Stays HTTP** — token refresh (off the 401-retry path by design) |
| `Login/Logout` | — | `lib/api/logout.ts` | **Stays HTTP** — auth |
| `Login/ActiveSession` | `IActiveSessionResult` | `routes/login/session-takeover.ts` | **Stays HTTP** — must *not* open a socket (that would trigger the takeover it warns about) |
| `Login/Status` | `IPlayerData` | `lib/engine/session.ts` | **Stays HTTP** — session-resume runs before the socket exists |
| `Login/DeviceInfo` | — | `lib/api/device-info.ts` | **Stays HTTP** — auth-adjacent telemetry |
| `Player` (`GET /api/Player`) | `IPlayerData` | **none** | **Dead — remove.** Player data reaches the client via `Login` (`LoginResult.player`) and `Login/Status` |
| `Statistics` (`GET /api/Statistics`) | `IPlayerStatistic[]` | `stores/statistics.svelte.ts` | **Migrate to socket** (player progress read) |
| `Challenges/Player` | `IPlayerChallenge[]` | `stores/challenges.svelte.ts` | **Migrate to socket** (player progress read) |
| `Statistics/StatisticTypes` | `IStatisticType[]` | **none** | **Dead — remove.** The client uses the `GetStatisticTypes` socket twin (`lib/engine/reference-data.ts`) |
| `Tags`, `Tags/TagCategories` | `ITag[]` / `ITagCategory[]` | admin workbench | **Out of scope** — admin-only reference reads with no socket equivalent yet (`backend.md` already tracks this) |
| `AdminTools/*` (`AddEdit*` / `Set*`) | — | admin workbench | **Stays HTTP** — admin persistence (documented exception) |
| `AdminTools/GetUsers`, `AdminTools/GetRoles` | search / `IRole[]` | admin user mgmt | **Out of scope** — admin-only reads, not the game client |

### Reference-data vs player-progress reads (the naming trap)

The socket already has `GetChallenges` (returns `IChallenge[]` — the **challenge definitions**,
reference data) and `GetStatisticTypes` (`IStatisticType[]` — **statistic metadata**). The two
HTTP endpoints to migrate return *player progress*: `IPlayerChallenge[]` (per-challenge
completion/progress) and `IPlayerStatistic[]` (the player's tracked statistic values). They are
distinct concerns, so the new commands must be named to avoid colliding with the existing
reference commands — proposed `GetPlayerStatistics` and `GetPlayerChallenges`.

## Findings

1. **Only two genuine game-client reads remain on HTTP:** `Statistics` and `Challenges/Player`.
   Everything else on HTTP is either auth (correctly excluded from the socket-everything goal),
   admin persistence (a documented HTTP exception), admin-only reference/user reads, or already
   dead.

2. **Two endpoints are already dead on the client and can be deleted:**
   - `GET /api/Player` (`PlayerController`) has no caller — player data flows through the auth
     responses (`Login` / `Login/Status`). `backend.md` currently calls this "safe to leave there";
     in practice it is unreferenced and should be removed.
   - `GET /api/Statistics/StatisticTypes` is superseded by the `GetStatisticTypes` socket command;
     only the socket twin is used.

3. **Front-loading at startup is already implemented.** `lib/engine/engine.ts` calls
   `statistics.load()` and `playerChallenges.load()` on game boot, and both stores coalesce
   concurrent callers onto one request (`stores/statistics.svelte.ts`,
   `stores/challenges.svelte.ts`). The screens additionally `load(true)` on mount to refresh the
   authoritative values. So the owner's "both data sets could load at startup" is done; migrating
   the transport to the socket does not change that.

4. **Targeted optimistic updates already exist** for the *gating-critical* state, which is the part
   that must feel instant:
   - `statistics.markZoneCleared(zoneId)` on a boss victory (`enemy-manager.ts`) so the "Cleared"
     seal appears immediately;
   - `playerChallenges.markCompleted(challengeId)` on the authoritative `ChallengeCompleted` server
     push (`engine.ts`) so zone-navigation unlocks immediately.
   Both reconcile against the server on the next `load(true)`. The server remains authoritative
   (it evaluates challenges and pushes completions; the client does not).

## Decisions (proposed)

1. **Migrate the two player-progress reads to socket commands** `GetPlayerStatistics` and
   `GetPlayerChallenges`. These are player-scoped reads, so — unlike the reference-data commands —
   they extend the ordinary player-command base and resolve the player from the socket session
   (`context.Session.SelectedPlayerId`), exactly as `StatisticsController` / `ChallengesController`
   do today via `SessionService.SelectedPlayerId`. They delegate to the same
   `IPlayerProgressRepository.GetStatistics` / `GetChallenges`, which is **already cache-first**
   after #550, so the socket read serves warm cached progress. The stores swap `ApiRequest.get(...)`
   for `fetchSocketData(...)` (which throws on socket error, preserving the stores' existing
   try/catch contract). This is a pure transport change with **no logic duplication** and no parity
   surface.

2. **Delete the two dead endpoints** (`GET /api/Player` + `PlayerController`; the HTTP
   `Statistics/StatisticTypes` action) once a final check confirms no remaining consumer. Update
   `backend.md` to drop the "only `GET /api/Player` remains" wording.

3. **Front-loading: nothing to do** — it already happens at boot (finding 3). The spike records
   this so it isn't re-implemented.

4. **Full per-stat / per-challenge optimistic recomputation: do NOT pursue ("not worth the
   squeeze").** Going beyond the current gating-only optimistic updates would mean porting
   `PlayerProgress.RecordBattleCompleted` + challenge evaluation to the client — duplicating the
   stat-row mapping, boss-only rules, and progress math, and creating a battle-logic-grade parity
   burden (a second place to keep in lockstep, with its own mirrored test matrix). The payoff is
   marginal: the full breakdown is only seen on the stats/challenges screens, which already
   `load(true)` authoritative values on open, and the gating-critical bits already update
   instantly. Recommendation: keep the targeted optimistic updates; if live-updating counters on
   the stats screen is ever wanted, scope it narrowly to the handful of always-visible counters
   with explicit server reconciliation — not a wholesale logic port.

## Implementation issues

Created as sub-issues of #473:

- **[#563](https://github.com/ginderjeremiah/GameServer/issues/563)** _(claude, tech debt, scope:
  small)_ — Add `GetPlayerStatistics` socket command and migrate `stores/statistics.svelte.ts` off
  HTTP. New player-scoped command delegating to `IPlayerProgressRepository.GetStatistics`; store
  swaps `ApiRequest.get('Statistics')` → `fetchSocketData('GetPlayerStatistics')`; codegen the type
  map; unit/integration tests mirroring the existing store test.
- **[#564](https://github.com/ginderjeremiah/GameServer/issues/564)** _(claude, tech debt, scope:
  small)_ — Add `GetPlayerChallenges` socket command and migrate `stores/challenges.svelte.ts` off
  HTTP. As above for `Challenges/Player`; name disambiguated from the existing reference
  `GetChallenges`.
- **[#565](https://github.com/ginderjeremiah/GameServer/issues/565)** _(claude, tech debt, scope:
  small)_ — Remove the unused `GET /api/Statistics/StatisticTypes` HTTP endpoint (keep the
  `GetStatisticTypes` socket command).
- **[#566](https://github.com/ginderjeremiah/GameServer/issues/566)** _(claude, tech debt, scope:
  small)_ — Remove the dead `GET /api/Player` endpoint and `PlayerController`; confirm no consumer,
  delete, update `backend.md`.
- **[#567](https://github.com/ginderjeremiah/GameServer/issues/567)** _(claude, tech debt, scope:
  small)_ — **Do last.** Once the four above land, prune the now-unused `Statistics`,
  `Challenges/Player`, `Player`, and `Statistics/StatisticTypes` entries from the HTTP type map and
  refresh `backend.md` → _HTTP vs WebSocket_ to reflect that the only client-game HTTP left is the
  auth flow (plus the admin persistence/Tags exceptions).

## Out of scope (recorded, not planned here)

- **Tags / TagCategories over HTTP** — admin-only reference reads; a socket migration is the same
  pattern but belongs with admin tooling, and `backend.md` already tracks them as the remaining
  HTTP reference exception.
- **`AdminTools/GetUsers` / `GetRoles`** — admin user-management reads, not the game client.
- **Admin persistence (`AdminTools/*` writes)** — a documented, intentional HTTP exception.
