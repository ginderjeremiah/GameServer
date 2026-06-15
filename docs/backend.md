# File Structure and Projects

The backend follows an onion-style architecture across several projects (each in a folder of the same name):

- **`Game.Core`** — the domain layer: domain objects/services, game mechanics, and the battle simulation. The heart of the game logic.
- **`Game.Application`** — orchestration only: coordinates repositories and domain services to fulfil use cases. No domain logic.
- **`Game.Api`** — ASP.NET Core controllers, WebSocket command handlers, and API models. Also hosts a reflection-based codegen that generates the frontend's API client. Handles HTTP/WebSocket requests and input validation only.
- **`Game.Abstractions`** — shared interfaces (repositories, infrastructure services) and the read/write contracts (`Game.Abstractions.Contracts`). EF entities do **not** live here.
- **`Game.DataAccess`** — repository implementations, the entity↔domain/contract mappers (`Mapping/`), and the in-memory reference-data caches.
- **`Game.Infrastructure`** — the Redis cache/pub-sub implementations, the EF `GameContext`, the **EF entity models** (`Game.Infrastructure.Entities`), and migrations.

Each layer has a matching `*.Tests` project. `Game.Infrastructure.Tests` is deliberately lightweight — only in-process logic with no out-of-process dependency; anything coupled to Redis or the database is covered by integration tests instead.

# General Backend Guidelines

- Repositories create and persist domain objects and dispatch domain events before persisting. They contain no domain logic and never return entity models or DB DTOs — mapping between domain objects, entities, and DTOs lives in `Game.DataAccess/Mapping`.
- Domain logic lives in `Game.Core`. The application layer only orchestrates; the API layer only handles requests, validates input, and returns responses.

## Testing Guidelines

Unit tests follow the classical (Detroit) school — the project has no unmanaged dependencies, so avoid test doubles. A class that depends on an out-of-process dependency (database, cache) should hold no logic worth unit-testing; if it does, move that logic into a domain class and cover the dependency interaction with integration tests instead.

Judge branch coverage per-branch rather than chasing the number: defensive guards (e.g. config that is always supplied) and branches reachable only by disrupting process-wide shared infrastructure are accepted as measure-only, with the real logic pinned by integration tests against the DI-resolved interface.

### Running tests (Microsoft.Testing.Platform)

The test projects run on **Microsoft.Testing.Platform** (MTP / xunit.v3), not the legacy VSTest runner. The gotcha: VSTest's `--filter` syntax is **silently ignored** (it sits before `--`, so the wrapper swallows it and the *whole* suite runs). Pass MTP filters after `--` instead:

```
dotnet test Game.Api.Tests/Game.Api.Tests.csproj -- --filter-class "*SessionServiceTests"
```

(`--filter-class` / `-method` / `-namespace` / `-trait`, each with a `--filter-not-*` negation; run the binary with `--help` to list them.) A filtered run is a dev convenience — CI runs the whole solution under coverage, so run the full suite before pushing.

# Important Architectural Design Decisions

## Entity model location and isolation

EF entity models are a persistence detail of the data tier and are **invisible to `Game.Api` and `Game.Application`**. They live in `Game.Infrastructure.Entities`, next to `GameContext`. `Game.DataAccess` references `Game.Infrastructure` with `PrivateAssets="Compile"`, so the entity types don't flow transitively up to `Game.Api`, and `Game.Application` never references `Game.Infrastructure` at all. Within the data tier itself the entity seam stays `internal` (the low-level insert/update/delete tracker and the entity-returning reference lookups); the public `Game.Abstractions` repository interfaces speak only domain models and contracts. An architecture test asserts that neither `Game.Api` nor `Game.Application` references `Game.Infrastructure`, failing fast if the wiring ever regresses.

## Reference Data

Reference data is either **intrinsic** or **static**:

- **Intrinsic** reference data is encoded in the application as an enum or similar (e.g. `EAttribute`, `EItemCategory`). It is persisted only for data integrity and is essentially never queried — it is served straight from the enum. Intrinsic sets may carry derived display metadata (classification, formatting) on the domain/contract directly, sourced from the enum with no database columns; adding such a field changes the set's version hash, so clients re-download it once.
- **Static** reference data (items, item mods, skills, enemies, zones, challenges) is authored content stored in the database and **cached in-memory as immutable lists, indexed by id**. The Id column is seeded from **0** so an entity's id doubles as its array index on both tiers — an O(1) lookup, but one that requires the ids stay contiguous.

The caches are immutable snapshots **loaded eagerly at startup** (a database problem surfaces as a boot failure, not a first-request failure) and refreshed by build-then-swap (see [Reference-data cache reload](#reference-data-cache-reload-build-then-swap)). There is no lazy-fill path.

> **Contiguity is guaranteed by retirement, not convention.** Because lookups index by id, a hard delete of a non-terminal record would open a permanent gap that silently mis-resolves every lookup above it. Top-level reference records are therefore **retired, not deleted** (see below).

### Retiring reference data (content lifecycle)

A top-level reference record carries a nullable `RetiredAt` timestamp — the same flag-plus-audit shape as the User soft-delete. Retiring takes a record out of circulation without erasing it; reinstating clears the timestamp.

- **The slot is permanent** — a retired record is never removed or renumbered, so the cache stays gap-free and every index lookup keeps resolving correctly.
- **It stays resolvable by id**, so existing references (an equipped item, a battle snapshot, an authored reward) keep working. Retirement is a catalogue/authoring concern only — it is not carried on the lean battle models, so a retired item a player owns fights identically.
- **It is out of circulation** — e.g. a retired enemy is dropped from the random per-zone spawn tables. There is no gameplay path that picks randomly across *all* reference data, so the circulation surface is small.
- **No hard delete** of a reference record: the admin "delete" affordance is Retire/Reinstate, and the reference admin repos reject a top-level delete outright.

**Tags are the deliberate exception.** They carry their own (non-zero-based) identity and are resolved by lookup, not by index, so they never suffer the mis-resolution bug retirement exists to prevent. Their admin "delete" stays a real hard delete (graceful for an in-use tag — the join rows cascade and the caches reload so the removed tag id disappears on the next read).

### Reference-data read contracts

The read path never exposes EF entities:

- **Reference reads return read contracts** (`Game.Abstractions.Contracts`) — one published language shared by the game client's loading screen and the admin Workbench. The internal cache stays as entities (the domain mappers need their fidelity) and is projected to contracts, preserving the id-as-index ordering.
- **Gameplay reads return lean `Game.Core` domain models**, not entities, and hand back **shared, pre-materialized instances** by id rather than re-mapping a fresh graph per call (these reads are on the per-battle hot path). This is safe because the reference-data domain models are structurally immutable value objects — `init`-only properties and `IReadOnlyList<>` collections, enforced by the compiler and guarded by a reflection test — so a consumer cannot grow or reassign the shared graph and corrupt the cache for every player. The level-parameterized enemy read is the same idea adapted: a level-independent template is shared and cloned at the requested level.
- **Admin writes keep entities inside the data tier** — the entity-returning lookups live on `internal` data-tier interfaces consumed only by the Content Authoring admin repositories (see [backend-admin.md](./backend-admin.md#admin-tools-api-surface-content-authoring-context)).

### Reference-data cache reload (build-then-swap)

Cache busting is an **eager build-then-swap** (stale-while-revalidate), not a null-and-lazily-refill (see the [cache-reload spike](./spikes/356-reference-data-cache-reload.md)). Readers never observe an empty or torn snapshot, and never pay a refill query inline.

- A **singleton holder** per set owns the current immutable snapshot and exposes a lock-free read plus `ReloadAsync()`. A reload builds the whole new snapshot off to the side (on its own DbContext) and publishes it with a single atomic reference swap, so a **failed reload leaves the old snapshot in place**. Derived structures (e.g. the per-zone spawn tables built from the enemy list) are bundled into the snapshot so they swap atomically and a reader can never see a new list against stale derived data. A per-holder semaphore serializes reloads to preserve read-your-writes.
- **An admin write triggers an awaited reload** after the write commits (so the Workbench reads its own writes with zero gap) and **broadcasts a cross-instance invalidation over the Redis backplane**. Other instances debounce the notification and run one background reload sweep; the publishing instance skips its own message. A failed background sweep is retried with backoff and never disturbs readers.

## Player-facing read projections

Player-facing reads also avoid exposing EF entities, but — unlike reference data — they reuse the existing **gameplay domain models** rather than dedicated contracts (those models are already lean enough), with the API DTOs projecting from them.

- **Statistics and challenge progress** go through the single `IPlayerProgressRepository`, which also owns the progress write path, giving one source of truth for player-progress data. A statistic's "no data yet" state is the **absence of its row**, never a magic `0` value — a stored value (0 included) is always a genuine recording. Min/max aggregation and the "at most" challenge path therefore key off row presence (`TryGetStatisticValue`), so a legitimate `0` minimum (e.g. an instant `FastestVictory`) records and counts correctly instead of being treated as empty.
- **The skill loadout is sent as the full unlocked set** (each skill with its `Selected`/`Order`), mirroring how inventory is sent as the full unlocked item set; the client derives the ordered equipped loadout from it. The loadout cap is a generated client constant, not a per-player wire field.
- **The loadout is edited through one atomic socket command** that replaces the whole equipped set in order (handling select, deselect, and reorder in one path). Like the other player mutations it validates as **anti-cheat** — no duplicate ids, count within the cap, every id already unlocked — and rejects with no state mutation on any violation.

## HTTP vs WebSocket Communication

**The goal: once the session is live, every game-client read and player-state mutation goes over the socket — only the auth flow stays on HTTP.** This is what lets the player's state be held in-memory on the backend and updated in real time without re-synchronizing against Redis or the database on each request. Users connect to one instance at a time, and a player's socket commands are handled sequentially. The goal has been reached; the HTTP that remains is deliberately scoped to:

- the **auth flow** (login, account creation, refresh, logout, status, active-session, device-info) — it runs before a socket exists, and the active-session check must deliberately *not* open one;
- admin **persistence** endpoints; and
- the admin-only reads that have no socket equivalent yet (tags, user/role lookups).

**Player-state mutations belong on the socket because the socket serializes them with the battle loop.** Player writes are read-modify-write against the in-memory aggregate; the socket read loop processes one command per player at a time, so a write is naturally serialized with the idle battle-completion commands that mutate the same cached player. The same write over HTTP would run concurrently on a separate thread and could silently clobber a background battle save. Every player-mutating action is therefore a socket command and validates as anti-cheat (rejecting with no mutation) — e.g. a stat allocation only accepts the core attributes that actually have an allocation row, which blocks allocating into derived attributes.

**The per-player sequential guarantee is enforced by a per-socket command lock**, because server-initiated pushes (challenge-completion, socket-replaced) arrive over the Redis backplane and run *off* the read loop — so two paths could otherwise mutate the same cached player concurrently, the exact lost-update class the serialization exists to prevent. Each command also runs under a per-command timeout, and that cancellation token is plumbed down through the application services and the data tier (EF/Npgsql honour it natively; the Redis client is wrapped to honour it cooperatively) so a wedged command unwinds promptly and releases the lock rather than stalling every later command. A separate per-socket send lock guards WebSocket output, since overlapping sends on one socket are illegal.

## Single active connection (session takeover warning)

A player may only be connected to one instance at a time: the socket manager records the player's current socket id in Redis and, when a new socket replaces an existing one, signals the old socket to close. To let the client warn the user **before** that takeover happens, the authenticated `Login/ActiveSession` endpoint reports whether the player already has a live socket — deliberately over **HTTP, not a socket command**, because opening a socket to ask the question would itself trigger the replacement it is meant to warn about. Because the check is presence-based it covers "another tab" and "another device" uniformly.

The presence key carries a short TTL refreshed on every inbound socket message (the client heartbeats every 10s), so a live connection stays fresh while a connection that vanished without a clean close expires within the TTL. The key is claimed atomically (set-with-TTL in one operation) and rolled back if the post-claim setup fails, so a partial registration can't leave a ghost session that blocks the player.

## Graceful socket drain on shutdown

A stopping instance drains its live sockets cleanly rather than leaving them to be force-killed — the missing lifecycle piece for an app designed to run as multiple scalable instances (rolling deploys, scale-down). A singleton registry tracks every live socket and uses its shutdown hook to drain them in two phases: first stop the per-socket inactivity watchdogs (teardown now owns the close), then send each socket a normal-closure close frame and await the loops within a bounded timeout, aborting any still-blocked receive past the deadline so the loop unwinds rather than waiting out the host timeout. The client treats the normal closure as a cue to reconnect — which a rolling deploy routes to a healthy instance — so no client change was needed.

## Reference-data caching and versioning

The frontend caches each reference-data set in the browser and re-downloads only the sets that changed (see `frontend.md`). The `GetReferenceDataVersions` socket command supports this by returning a **content hash per set** in one round-trip. The hash is computed on demand by hashing the same serialized models the client actually receives (SHA-256 over the camelCase JSON), so the version changes if and only if the client-visible data changes — there is no separate version counter to maintain or invalidate. The versioned set is exactly the set of reference-data socket commands (discovered by assembly scan), so a newly-added one is versioned automatically.

## Battle (setup, runtime & skill effects)

Setting up and running a battle is a domain decision, not an orchestration one: the application layer only snapshots the player and the chosen enemy loadout, sets the active battle, and delegates the game-logic choices to `Game.Core`. **Parity invariant:** battle logic runs on both the frontend and backend and the results must be identical — the backend replays the client's reported battle as anti-cheat — so these mechanics are mirrored on both sides and pinned by a parity matrix; they must never diverge. The tick-by-tick detail — encounter setup, snapshot reconstruction, zone-unlock anti-cheat, the RNG seed, and the skill-effect/DoT-HoT runtime — lives in [backend-battle.md](./backend-battle.md).

## Caching, Pub/Sub & write-behind persistence

Most reference data is cached in-memory (see [Reference Data](#reference-data)), but **player data uses a write-behind cache where Redis is the source of truth**: a live player's aggregate is loaded once onto the connection-scoped session, mutated in-memory per socket command, and persisted to Postgres asynchronously through a Redis queue drained by a background service. Player progress (statistics + challenges) is a second such write-behind aggregate under its own key, and Redis pub/sub doubles as the WebSocket backplane and the carrier for cross-instance reference-data invalidations. The deep mechanics — the sliding TTLs, the domain-event → batched-publish path, dead-lettering and retry, drain serialization, and worker ownership — live in [backend-persistence.md](./backend-persistence.md).

## Challenge-completion notifications (server push)

Completing a challenge unlocks its rewards as a side effect of battle-completion handling, decoupled from the socket command that triggered the battle — so the command's response can't carry the unlock, and it would otherwise surface only on the next page load (the client doesn't evaluate challenges itself; the server is authoritative). Instead the player aggregate raises a challenge-completed event and an API-layer handler pushes a server-initiated socket command to the player over the Redis backplane, so it reaches them on whichever instance holds their connection and covers **every** completion path (idle victory, won-abandon, even a loss that completes a counting challenge). Being server-initiated, the push carries a typed response but no request parameters, so codegen emits no client request type. A push that fails is logged and the queue keeps draining; there is no dead-lettering or client surfacing for pushes yet.

## Authentication

JWT bearer auth, not server sessions. Access tokens are signed (HMAC-SHA256), short-lived (15 min), and carry the user id + role claims; refresh tokens are opaque, long-lived (48h), single-use, and rotate on every refresh. Refresh tokens live server-side in Redis as a hash — chosen because auth already depends on Redis, so it adds no new durability assumption.

The **token is the sole authorization source of truth** (roles are read off the validated principal, never the session cache), so a role change only takes effect on next login. A global fallback policy requires every endpoint to be authenticated unless explicitly marked anonymous. WebSockets authenticate via an `access_token` query-string parameter, since browsers can't set headers on the WebSocket handshake. The frontend owns token persistence, pre-emptive refresh, and the single-flight refresh path (see `frontend.md`).

**The validated token is likewise the sole authority for whether a caller is *authenticated* — never the gameplay session cache.** That cache (`Session_{userId}` → the in-flight `PlayerState`) is a volatile presentation convenience holding the in-flight battle state and the user→player binding; it can be evicted (Redis flush, sliding-TTL lapse) or simply never established on a given instance. A valid token with no cached session is therefore *authenticated-but-uncached*, not anonymous: `SessionLoaderMiddleware` rehydrates the session from the database (re-deriving the player binding the same way login does) instead of reporting "not logged in". `SessionService.Authenticated` is gated only on the token-derived `UserId`; a cache hit/miss governs `HasPlayerSession`, never authentication.

### Password hashing

Passwords use **PBKDF2-HMAC-SHA256** behind an `IPasswordHasher` abstraction. A hash is stored in a self-contained, tunable format carrying the iteration count and a per-hash salt (default 600k iterations, the OWASP floor); an application-wide pepper is folded in via HMAC and validated at startup so a missing pepper fails fast. Verification is constant-time, and a valid credential stored with an outdated work factor is transparently re-hashed on next login.

### Account/login orchestration

Account creation and the login/refresh/logout flows are orchestrated by `AccountService` in the application layer; the controller is a thin HTTP adapter that maps the result and wires the request-scoped session (a presentation concern). The new-player defaults (starter skills, base attributes, default preferences) are a domain concern built by a `NewPlayerFactory` in `Game.Core`, so the application layer neither encodes the defaults nor constructs entity graphs. Access-token issuance sits behind an `IAccessTokenService` abstraction so the application layer stays free of the JWT libraries, while the concrete implementation lives at the presentation edge alongside the bearer-validation pipeline.

At most one active account may hold a given username, enforced by a **partial unique index** (`Username WHERE ArchivedAt IS NULL`) so two concurrent creations can't both slip past the up-front availability check and insert duplicate active rows. The filter excludes archived users, preserving username reuse after archival. Account creation therefore commits its own insert in the data tier (rather than deferring to the per-request unit of work) so the index's unique-violation surfaces as a clean "username taken" result instead of a 500 raised after the action returns.

## CORS allowed origins (deployment config)

The browser CORS policy's allowed origins are **configuration-bound, not hardcoded** — they are deployment-specific. The list supports multiple origins and is validated at startup (must be non-empty), so a misconfigured environment fails fast rather than silently rejecting every browser request. The local dev origin ships in the Development config.

## Access Roles and Admin Authorization

Authenticated users are gated out of admin tooling unless they hold the `Admin` role. **Roles live in the signed auth token, not the session store** — the token is the source of truth for what a request is authorized to do, which keeps the check self-contained and avoids a second lookup; the trade-off is that a role change only takes effect on the user's next login. The admin authorization filter reads the role straight off the cryptographically validated principal, **never** off the gameplay session cache: that cache is a presentation concern decoupled from the token's validity, so keying off it would wrongly reject a valid admin whose session key happens to be absent (evicted, never established, or aged out).

## Admin tooling & user administration

Reference-data administration is modelled as a distinct **Content Authoring** bounded context (thin admin controllers → admin repositories that own persistence; the EF entity never surfaces in `Game.Api`), and user/role management is a separate **Identity / User Admin** context that shares the `User` aggregate with the auth flow. Both speak `Game.Abstractions.Contracts` types rather than entities, and every admin write reloads the in-memory reference caches. Users are archived (a soft delete that frees the username) or banned (blocked but name reserved). The API surface, the change-set/reconciler persistence patterns and their zero-based-id gotchas, the archive-vs-ban model, and the connection-tracking tables live in [backend-admin.md](./backend-admin.md).

## Build & Test Infrastructure

The build/test/CI tooling that supports the backend — the standalone TypeScript client codegen, the integration-test container strategy for constrained environments, and the Dockerized API stack for end-to-end (Playwright) runs — lives in [infrastructure.md](./infrastructure.md).
