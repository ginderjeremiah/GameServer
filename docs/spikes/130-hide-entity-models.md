# Spike #130 — Hide EF entity models from the API and Application layers

- **Spike issue:** [#130](https://github.com/ginderjeremiah/GameServer/issues/130)
- **Status:** Complete — approach decided, implementation issues filed (#133–#138).

## Goal

Decouple `Game.Api` and `Game.Application` from the EF entity models (currently
`Game.Abstractions/Entities`), and stop the repository interfaces from trafficking in
entities — they should speak domain models / contracts instead. The entity model should
be an implementation detail of the data tier.

## Current-state inventory (where the coupling lives)

**Repository interfaces (`Game.Abstractions/DataAccess`) — 14 of 21 leak entities:**

- Leaky: `IItems`, `ISkills`, `IEnemies`, `IZones`, `IItemMods` (mixed — entity `All()` +
  domain `Get*`), plus `IItemCategories`, `IItemModTypes`, `ITags`, `ITagCategories`,
  `IRoles`, `IUsers`, `IPlayerStatistics`, `IPlayerChallenges`, and the generic
  escape-hatch `IEntityStore`.
- Already clean (the target shape): `IPlayerRepository`, `IPlayerProgressRepository`,
  `IChallenges`, `ISessionStore` speak `Game.Core` types; `IRefreshTokenStore`,
  `IUserLogins`, `IDatabaseMigrator` are entity-free.

**`Game.Api` — three clusters:**

1. ~22 read DTOs in `Game.Api/Models/*` whose `static FromSource(entity)` factories take
   EF entities as input (e.g. `Game.Api/Models/Items/Item.cs`). The wire shape is already a
   DTO; it is the *mapping* code that depends on entities.
2. 8 admin controllers (`Game.Api/Controllers/Admin/*`) constructing ~16 entity types
   inline and pushing them through `IEntityStore`.
3. A handful of non-admin controllers / socket commands that pipe a repo's entity result
   into `.To().Model<T>()`.

**`Game.Application` — only two services:**

- `AccountService` builds the `User`+`Player`+`PlayerSkill`+`PlayerAttribute` entity graph
  (the player-graph half is already tracked by #126).
- `BattleService.StartBattle` reads the **entity** `Zone`'s `LevelMin/LevelMax` (related to
  #118). `PlayerService` is already clean — it uses the **domain** `LogPreference`.

## The core tension

The intuitive idea — *make repositories return domain models and let admin write through
them* — runs into three facts established during the spike:

1. **Domain models are deliberately lossy gameplay projections.** `ItemMapper` drops
   `Tags`, discards child-row identities (`ItemAttribute.Id`), and flattens attributes into
   `AttributeModifier`. They lack fields reads need (`IconPath`), and `Enemy` is
   **level-bound** (`GetDomainEnemy(id, level)`), not a catalogue template.
2. **Only `Player` is an `AggregateRoot`.** Reference-data domain models have **no domain
   events and no persistence machinery** — the write-behind pipeline is Player-only, and
   there are **zero domain→entity mappers** (everything is one-way entity→domain for reads).
3. **The admin write code is intrinsically EF-aware** (fresh navigation-free entities to
   dodge graph-drag, zero-based-identity update semantics, `Delete→Edit→Add` ordering, cache
   invalidation). This is CRUD/data-management, not gameplay.

Forcing admin through gameplay aggregates would mean de-lossifying every model, turning each
into a write-capable aggregate with events + handlers, and writing bidirectional mappers —
heavy work that pollutes the gameplay domain. That makes the "separate persistence path for
admin" the pragmatic fit.

## Decisions

1. **Admin is modelled as two bounded contexts** (not forced through the gameplay domain):
   - **Content Authoring** — reference-data CRUD (items, mods, skills, enemies, zones,
     challenges, tags + their relationships). These are too interconnected to split further,
     so they share one context. The signal is textbook DDD: the same table means two
     different things in two languages — in gameplay an `Item` is a behavioural object for
     battle simulation; in authoring it is an *editable content record* (full fidelity,
     identity preserved, validation on save).
   - **Identity / User Admin** — users, roles, archive/ban. Near-zero overlap with content;
     shares the `User` aggregate with the existing auth/login flow, so it sits alongside it.

   Each context gets its own model over the shared database. The EF entity becomes an
   implementation detail of `Game.DataAccess`; the admin controllers talk to admin
   DTOs/contracts only.

2. **Reads return dedicated read contracts** — a single *published language* shared by the
   game client's loading screen and the admin Workbench (which already share the `Get*`
   socket commands). The entity→contract mapping moves **down into `Game.DataAccess`**
   (consistent with backend.md placing DTO mapping in the data tier). Gameplay domain models
   stay lean; only the **write** contracts are admin-context-specific.

3. **Entities relocate into `Game.Infrastructure`** (next to `GameContext`), with **no
   `internal` needed**: `Game.DataAccess.csproj` already references `Game.Infrastructure`
   with `PrivateAssets="Compile"`, so Infrastructure's compile-time types do not flow
   transitively to `Game.Api`, and `Game.Application` never references Infrastructure at all.
   An architecture test (`Game.Api` / `Game.Application` have no entity dependency) is added
   as a cheap regression guard.

4. **Physical layout:** namespaces/folders within the existing projects (no new projects).
   Admin controllers already live under `Game.Api/Controllers/Admin`; admin application
   services and admin repositories go under `Admin` namespaces in `Game.Application` /
   `Game.Abstractions` / `Game.DataAccess`.

## Target architecture (representations)

| Representation | Project | Role |
| --- | --- | --- |
| EF entity | `Game.Infrastructure` (after relocation) | Persistence only; invisible to API/App |
| Domain model | `Game.Core` | Gameplay (lean, Player is the only aggregate) |
| Read contract | `Game.Abstractions` (contracts namespace) | Published read language for client + Workbench |
| Admin write contract | existing `Game.Api/Models` admin request DTOs | Content-Authoring / Identity write language |

## Implementation issues

Tracked as sub-issues of #130:

1. #133 — Reference-data reads: read contracts + move entity→model mapping into `Game.DataAccess`.
2. #134 — Player-facing reads (statistics, challenges, inventory, log preferences).
3. #135 — Content Authoring context: dedicated admin persistence for reference-data CRUD.
4. #136 — Identity / User Admin context: entity-free user administration.
5. #137 — Application-layer cleanup (`AccountService`, `BattleService`); coordinates with #126 and #118.
6. #138 — **Capstone:** relocate entities into `Game.Infrastructure`, internalize
   `IEntityStore`, add the architecture test. Must land last (the test fails until the others merge).

Issues 1–5 are largely independent; #138 is the capstone.

## Notes / follow-ups surfaced

- **Reference-data version hash:** computed over the serialized model
  (backend.md → *Reference-data caching and versioning*). Keeping the contract's serialized
  shape identical to today's DTOs avoids a re-download; otherwise it is a one-time,
  intentional cache bump.
- **`PlayerState` entity may be vestigial.** Session state is the **domain** `PlayerState`
  in Redis (`ISessionStore`, `SessionStore`); the `Game.Abstractions/Entities/PlayerState`
  table looks unused at runtime. Worth verifying and possibly removing during #134/#138 —
  not pursued in this spike.
- backend.md's *Admin Tools API surface* section should be updated once #135/#136 land to
  describe the bounded-context split.
