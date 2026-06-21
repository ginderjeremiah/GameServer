# Spike #922 — Multiple players per account

- **Spike issue:** [#922](https://github.com/ginderjeremiah/GameServer/issues/922)
- **Status:** Research complete; direction decided with the project owner; split into implementation sub-issues [#1068–#1072](#implementation-issues) plus follow-ups [#1073](#follow-ups) and [#1074](#follow-ups). Not yet implemented.

## Goal

Let a single account own multiple player characters, add a **character-select screen** to the login flow, and decide whether a live game session is scoped **one-per-user** or **one-per-player**.

## How it works today (what the feature builds on)

The backend was built in anticipation of this — the data layer is already one-to-many, and the binding logic has a deliberate seam:

- **The DB relationship is already 1:N.** `Player.UserId` is a **non-unique** FK with a plain `IX_Players_UserId` index, and `User.Players` is a collection navigation. No schema migration is needed for the relationship itself.
- **The binding seam already exists.** `AccountService.SelectPlayerId(playerIds)` returns `playerIds[0]` today and its own doc comment names it the future player-selection seam. Both login and session-rehydration (`ResolveSelectedPlayerId`) route through it, so they can't diverge.
- **The read contract is already plural.** `AccountCredentials.PlayerIds` is a list; `Users.GetPlayerIds(userId)` already returns every player id for an account.
- **Cache keying is split.** The session (`Session_{userId}` → `PlayerState`, holding the bound player + in-flight battle) is keyed by **userId** — the single place that enforces one-session-per-account. The socket presence (`PlayerSocket_{playerId}`), the live player aggregate (`Player_{playerId}`), and player progress (`Progress_{playerId}`) are all already keyed by **playerId**.

So with one player per account today, "one session per user" and "one socket per player" coincide; multiple players is what makes them diverge — and that divergence is the core decision.

## The decisive constraint — client-driven loops can't run in parallel background tabs

The idle loop is driven client-side by `setInterval` in `logical-engine.ts`, and it **clamps and discards** over-budget time: each invocation only ever simulates up to `tickSizeX5` (200 ms) of battle, advancing the clock past any excess without simulating it. Browsers throttle hidden-tab timers (≥1 s, ~once/minute after sustained backgrounding), so a backgrounded character's loop processes ≤200 ms of battle per real second and discards the rest — and the loss is not recoverable via offline rewards, because the socket stays alive (the character isn't "away").

**Consequence:** "multiple live tabs" does **not** deliver simultaneous full-rate idling — only the foreground tab runs at speed. Genuine parallel progression would require moving the idle loop **server-side** (the server becomes the authoritative idle driver, not just the anti-cheat verifier), which is a major architectural shift well beyond this feature. This reframes the session-scope question away from "let multiple players run at once" toward "make switching between players lossless."

## Decisions

Settled with the project owner during the spike:

1. **One active session per account (Option A++): one character live at a time.** Switching characters is the way to play another. Chosen over per-player parallel sessions because (a) client-driven loops can't actually progress in parallel background tabs (above), so per-player parallelism would need server-side idle simulation; (b) the game has **no competitive surface** (no PvP/leaderboards/trading found), so faster self-progression harms no one and there is no balance reason to either block or build parallel farming; and (c) it preserves the deliberate "one instance / one active connection per user" simplification. Keeps `Session_{userId}` as the session key.

2. **Lossless character switching makes A++ outcome-equivalent to parallel progression.** The departed character is credited for its entire away period via the **#879 elapsed-time simulator** (idle battles are stationary i.i.d. draws, so a full replay is exact — see the [offline-rewards spike](./879-offline-rewards.md)), anchored on the `Player.LastActivity` maintained by #1039. The **≥5-minute floor is dropped for a deliberate switch** (the floor is a login-time concern). No partial-battle simulation is needed — the sim runs whole battles and the entered character resumes with a fresh one. The only thing this does *not* reproduce versus literal parallelism is rendering two battles on screen at once, which has no idle-game value.

3. **The selected player is carried as a token claim.** A new `SelectPlayer` step issues/rotates the access/refresh token pair to carry the chosen `playerId`, and rehydration reads it from the validated token instead of defaulting to `playerIds[0]`. This fixes a real correctness bug — today an evicted session would silently snap a multi-player account back to its first character — and fits the existing principle that the token is the sole authority for what a request may do (roles already work this way). The socket handshake then reads its player from the token, making the binding explicit and stateless.

4. **The login flow is split around selection.** `Login` verifies credentials and returns `{ tokens, playerSummaries }`; a new authenticated `SelectPlayer(playerId)` validates ownership, establishes the binding, and rotates the token. A new **character-select screen** sits between the login page and the loading screen, and also hosts character creation. The session-takeover (`ActiveSession`) check moves to run **after** selection, since it is a per-player presence check.

5. **Characters are created post-signup with a cap and their own names.** A `CreatePlayer(name)` endpoint builds a player via the existing `NewPlayerFactory` and attaches it to the account, bounded by a **config-bound per-account cap** (proposed default: 6) enforced server-side. Each character takes a **user-supplied name** (today the player name is the account username); names need not be globally unique. Deleting/retiring a character is deferred.

6. **Out of scope — deferred.** Server-side parallel idle (genuine simultaneous progression) is its own future spike. Recovering idle time lost to background-tab throttling for a *single* character is a pre-existing problem surfaced here and split into its own spike (#1073) — the natural fix runs the simulator **mid-session**, which #879 deliberately avoided, so it needs a design pass; the cheap interim mitigation (a browser allowlist nudge) is #1074.

## Flow (end to end)

**Login → enter a character**

1. Login page → `Login` (HTTP) verifies credentials and returns `{ tokens, playerSummaries }`.
2. Character-select screen shows the account's characters (and a "create character" affordance, bounded by the cap).
3. Selecting one → `SelectPlayer(playerId)` validates ownership, binds the session, and rotates the token to carry the `playerId` claim.
4. Session-takeover (`ActiveSession`) check runs for the selected character; then the loading screen, socket handshake (player read from the token), and game shell proceed as today.

**Switch character (in-game, no re-login)**

1. Switch affordance → tear down the current socket/session.
2. Backend runs the elapsed-time simulation for the **departed** character from its `LastActivity` (no 5-min floor), applies rewards, persists.
3. `SelectPlayer` binds + loads the newly chosen character; the entered character's catch-up summary (reusing the #879 welcome-back UI) is shown; the game re-initializes.

## Implementation issues

Created as sub-issues of #922.

**Dependency order:** #1068 is the foundation; #1069 and #1070 build the create/select surface on it; #1071 (lossless switch) additionally depends on **#1042** (the offline-rewards orchestration/simulator, itself depending on #1039–#1041); #1072 depends on #1071 and #1070.

- **[#1068](https://github.com/ginderjeremiah/GameServer/issues/1068) — Player-selection seam & auth flow** *(enhancement, claude, scope: medium)*. Split `Login` into verify → `{ tokens, playerSummaries }`; add `SelectPlayer(playerId)` (ownership-validated, binds the session, rotates the token with a `playerId` claim); resolve the selected player from the claim instead of `playerIds[0]`; add an `Id` to the player DTOs; move the takeover check post-selection.
- **[#1069](https://github.com/ginderjeremiah/GameServer/issues/1069) — Character creation & per-account cap** *(enhancement, claude, scope: medium)*. `CreatePlayer(name)` via `NewPlayerFactory`; config-bound per-account cap (default 6) enforced server-side; per-player user-supplied names (not globally unique). Delete/retire deferred. Depends on #1068.
- **[#1070](https://github.com/ginderjeremiah/GameServer/issues/1070) — Player-select & create-character screen (frontend)** *(enhancement, claude, scope: medium)*. New screen between login and loading; wire `SelectPlayer`; create-character affordance; relocate the takeover confirmation; re-init `playerManager` for the selected character. Depends on #1068 + #1069.
- **[#1071](https://github.com/ginderjeremiah/GameServer/issues/1071) — Lossless character switch (backend)** *(enhancement, claude, scope: medium)*. On switch, credit the departed character via the elapsed-time simulator from its `LastActivity` (5-min floor dropped), then bind + load the new one. Reuses the #1042 simulator; no partial-battle simulation. Depends on #1068 + **#1042**.
- **[#1072](https://github.com/ginderjeremiah/GameServer/issues/1072) — In-app character switcher (frontend)** *(enhancement, claude, scope: medium)*. Switch affordance that runs the switch flow, shows the entered character's catch-up summary (reusing the #879 welcome-back UI), and re-initializes the game. Depends on #1071 + #1070.

## Follow-ups

Surfaced by the spike; tracked separately, not blocking the feature.

- **[#1073](https://github.com/ginderjeremiah/GameServer/issues/1073) — Spike: recover idle time lost to background-tab throttling** *(spike, enhancement, scope: large)*. A pre-existing single-character problem: the logical engine discards over-budget time (`tickSizeX5` clamp) and there is no `visibilitychange` handling, so a backgrounded tab silently loses idle progress. Key insight: the in-flight battle is **deterministically reconstructable** from the inputs `PlayerState` already persists (`ActiveEnemy*`, `BattleSeed`, `Snapshot`, `BattleStartTime`), so resuming needs **no mid-battle state serialization** — replay from the seed to elapsed `T`. Mechanism: resolve the in-flight battle (credit it if it concluded), credit whole intervening battles via the #879 sim, then **carry the trailing remainder forward** as the next battle's start offset for the client to fast-forward and continue live (which also closes #879's boundary-slice loss). The net-new primitive is starting a battle at a non-zero offset; the cheap in-memory path (a quick tab-out) can fast-forward client-side without a round-trip. No `claude` tag: serious design considerations.
- **[#1074](https://github.com/ginderjeremiah/GameServer/issues/1074) — Allowlist-as-always-active nudge** *(enhancement, claude, scope: small)*. Interim mitigation for #1073: detect significant background time-loss and nudge the user to add the game to their browser's "never sleep" list (Chrome/Edge Memory Saver; note Firefox/Safari where applicable).

## Notes / follow-ups surfaced

- **Documentation to update on landing:** `docs/backend.md` (the account-login orchestration and single-active-connection sections — the split login + `SelectPlayer` + `playerId` token claim), `docs/frontend-screens.md` (the character-select screen and the in-app switcher), and `docs/game-design.md` (a "multiple characters per account" note: one live at a time, switching credited losslessly via the offline simulator).
- **Dependency on offline rewards.** The lossless-switch path (#1071) and the switcher summary (#1072) reuse the #879 simulator, so they land after #1042. Multiple-players and offline-rewards are coherent siblings (one simulator), not competitors.
- **Reversibility.** Because the heavy caches (player, progress, socket presence) are already per-player and the token now carries `playerId`, moving to true per-player parallel sessions later (with server-side idle) would not throw this work away.
